using System.Diagnostics;
using System.Net.Http.Json;
using System.Text.Json.Serialization;

namespace ERP.Watchdog;

/// <summary>
/// The SKYRA Watchdog Worker — the heartbeat of the ERP.
/// Runs as a Windows Service 24/7.
/// Responsibilities:
///   1. Self-Healing: Monitors the ERP process and restarts it if it crashes.
///   2. Auto-Update: Checks for new versions and applies them on a schedule.
/// </summary>
public class WatchdogWorker : BackgroundService
{
    private readonly ILogger<WatchdogWorker> _logger;
    private readonly HttpClient _http;

    private const string ErpProcessName = "SkyraERP";
    private const string InstallDir = @"C:\SKYRA";
    private const string VersionUrl = "https://raw.githubusercontent.com/fethi17891789/erp_pfc_20252026/refs/heads/master/version.json";
    private const string CurrentVersionFile = @"C:\SKYRA\version.txt";

    private string erpExe = Path.Combine(InstallDir, "ERP", "SkyraERP.exe");

    // Self-Healing check every 5 seconds
    private readonly TimeSpan _healthCheckInterval = TimeSpan.FromSeconds(5);
    // Update check every 6 hours
    private readonly TimeSpan _updateCheckInterval = TimeSpan.FromHours(6);

    private DateTime _lastUpdateCheck = DateTime.MinValue;
    private bool _updateReady = false;
    private string? _pendingUpdateZipPath = null;

    public WatchdogWorker(ILogger<WatchdogWorker> logger)
    {
        _logger = logger;
        _http = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("SKYRA Watchdog démarré.");

        while (!stoppingToken.IsCancellationRequested)
        {
            // ── Self-Healing ────────────────────────────────────────
            await EnsureErpIsRunningAsync();

            // ── Auto-Update (every 6 hours) ─────────────────────────
            if (DateTime.UtcNow - _lastUpdateCheck > _updateCheckInterval)
            {
                _lastUpdateCheck = DateTime.UtcNow;
                await CheckForUpdatesAsync();
            }

            // ── Apply update at 3:00 AM ─────────────────────────────
            if (_updateReady && IsUpdateWindow())
            {
                await ApplyUpdateAsync();
            }

            await Task.Delay(_healthCheckInterval, stoppingToken);
        }
    }

    // ════════════════════════════════════════════════════════════
    // SELF-HEALING
    // ════════════════════════════════════════════════════════════
    private async Task EnsureErpIsRunningAsync()
    {
        var processes = Process.GetProcessesByName(ErpProcessName);
        if (processes.Length > 0) return; // ERP is alive — nothing to do

        _logger.LogWarning("⚠ ERP SKYRA non détecté ! Relance en cours...");

        // On attend que le Bootstrapper ait au moins créé le fichier de config
        var configPath = Path.Combine(InstallDir, "appsettings.Production.json");
        if (!File.Exists(configPath))
        {
            _logger.LogInformation("Attente de la configuration initiale par le Bootstrapper...");
            return;
        }

        try
        {
            // Clean temp files
            var tempDir = Path.Combine(InstallDir, "temp");
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, recursive: true);

            // Check if PostgreSQL is running, restart if needed
            await EnsurePostgresRunningAsync();

            // Restart the ERP
            if (File.Exists(erpExe))
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = erpExe,
                    WorkingDirectory = InstallDir,
                    UseShellExecute = false
                });
                _logger.LogInformation("✓ ERP SKYRA relancé avec succès (Self-Healing).");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erreur lors du Self-Healing.");
        }

        await Task.Delay(2000); // Give it time to start
    }

    private async Task EnsurePostgresRunningAsync()
    {
        var pgCtl = Path.Combine(InstallDir, "Database", "PostgreSQL", "bin", "pg_ctl.exe");
        var dataDir = Path.Combine(InstallDir, "Database", "data");
        var logFile = Path.Combine(InstallDir, "Database", "postgres.log");

        if (!File.Exists(pgCtl)) return;

        var pgProcesses = Process.GetProcessesByName("postgres");
        if (pgProcesses.Length > 0) return;

        _logger.LogWarning("PostgreSQL non actif — redémarrage...");
        var process = Process.Start(new ProcessStartInfo
        {
            FileName = pgCtl,
            Arguments = $"start -D \"{dataDir}\" -l \"{logFile}\" -w",
            UseShellExecute = false,
            CreateNoWindow = true
        });
        process?.WaitForExit(20_000);
        await Task.Delay(2000);
    }

    // ════════════════════════════════════════════════════════════
    // AUTO-UPDATE
    // ════════════════════════════════════════════════════════════
    private async Task CheckForUpdatesAsync()
    {
        try
        {
            _logger.LogInformation("Vérification des mises à jour sur {Url}...", VersionUrl);

            var remoteInfo = await _http.GetFromJsonAsync<VersionInfo>(VersionUrl);
            if (remoteInfo == null) return;

            var currentVersion = GetCurrentVersion();

            if (Version.Parse(remoteInfo.Version) > Version.Parse(currentVersion))
            {
                _logger.LogInformation("Nouvelle version disponible : {NewVersion} (actuel: {Current})",
                    remoteInfo.Version, currentVersion);

                // Download the update package in background
                var zipPath = Path.Combine(InstallDir, "update.zip");
                var success = await WebDownloader.DownloadFileAsync(remoteInfo.DownloadUrl, zipPath);
                
                if (success)
                {
                    _pendingUpdateZipPath = zipPath;
                    _updateReady = true;
                    _logger.LogInformation("Mise à jour téléchargée. Application prévue à 3:00.");
                }
            }
            else
            {
                _logger.LogInformation("SKYRA est à jour (v{Version}).", currentVersion);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Impossible de vérifier les mises à jour (pas de connexion ?).");
        }
    }

    private async Task ApplyUpdateAsync()
    {
        if (string.IsNullOrEmpty(_pendingUpdateZipPath) || !File.Exists(_pendingUpdateZipPath))
        {
            _updateReady = false;
            return;
        }

        _logger.LogInformation("Application de la mise à jour SKYRA...");

        try
        {
            // 1. Backup PostgreSQL
            await BackupDatabaseAsync();

            // 2. Stop ERP gracefully
            foreach (var proc in Process.GetProcessesByName(ErpProcessName))
            {
                proc.Kill(entireProcessTree: true);
                await proc.WaitForExitAsync();
            }
            await Task.Delay(1000);

            // 3. Extract new files
            var backupDir = Path.Combine(InstallDir, "_backup");
            if (Directory.Exists(backupDir)) Directory.Delete(backupDir, true);
            Directory.Move(InstallDir, backupDir); // Move current to backup

            Directory.CreateDirectory(InstallDir);
            System.IO.Compression.ZipFile.ExtractToDirectory(_pendingUpdateZipPath, InstallDir);

            // 4. Restart ERP
            await EnsureErpIsRunningAsync();

            File.Delete(_pendingUpdateZipPath);
            _updateReady = false;
            _pendingUpdateZipPath = null;
            _logger.LogInformation("✓ Mise à jour SKYRA appliquée avec succès.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erreur lors de l'application de la mise à jour. Rollback en cours...");
            // Rollback: restore backup
            try
            {
                if (Directory.Exists(InstallDir)) Directory.Delete(InstallDir, true);
                Directory.Move(Path.Combine(InstallDir, "_backup"), InstallDir);
                await EnsureErpIsRunningAsync();
                _logger.LogInformation("Rollback effectué. Version précédente restaurée.");
            }
            catch (Exception rex)
            {
                _logger.LogCritical(rex, "Échec du rollback ! Intervention manuelle requise.");
            }
        }
    }

    private async Task BackupDatabaseAsync()
    {
        var pgDump = Path.Combine(InstallDir, "Database", "PostgreSQL", "bin", "pg_dump.exe");
        if (!File.Exists(pgDump)) return;

        var backupDir = Path.Combine(InstallDir, "Database", "Backups");
        Directory.CreateDirectory(backupDir);

        var backupFile = Path.Combine(backupDir,
            $"skyra_backup_{DateTime.Now:yyyyMMdd_HHmmss}.sql");

        var process = Process.Start(new ProcessStartInfo
        {
            FileName = pgDump,
            Arguments = $"-U postgres -d postgres -f \"{backupFile}\"",
            UseShellExecute = false,
            CreateNoWindow = true
        });
        if (process != null)
            await process.WaitForExitAsync();

        _logger.LogInformation("Backup de la base de données créé : {File}", backupFile);
    }

    private bool IsUpdateWindow()
    {
        var now = DateTime.Now;
        // Apply update between 3:00 AM and 3:05 AM to minimize disruption
        return now.Hour == 3 && now.Minute < 5;
    }

    private string GetCurrentVersion()
    {
        if (File.Exists(CurrentVersionFile))
            return File.ReadAllText(CurrentVersionFile).Trim();
        return "0.0.0";
    }
}

public class VersionInfo
{
    [JsonPropertyName("version")]
    public string Version { get; set; } = "0.0.0";

    [JsonPropertyName("downloadUrl")]
    public string DownloadUrl { get; set; } = "";

    [JsonPropertyName("changelog")]
    public string Changelog { get; set; } = "";
}
