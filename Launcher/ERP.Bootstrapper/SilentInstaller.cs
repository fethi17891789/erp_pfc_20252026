using System.Diagnostics;

namespace ERP.Bootstrapper;

/// <summary>
/// Handles silent (invisible) installation of missing dependencies.
/// Extracts bundled portable versions from the Resources folder.
/// </summary>
public static class SilentInstaller
{
    private static readonly string ResourcesPath = Path.Combine(
        AppContext.BaseDirectory, "Resources");

    /// <summary>
    /// Installs PostgreSQL Portable by extracting from the bundled zip.
    /// </summary>
    public static bool InstallPostgresPortable(string installDir)
    {
        Console.WriteLine("[INSTALL] Installation de PostgreSQL Portable en cours...");

        var pgZip = Path.Combine(ResourcesPath, "postgresql_portable.zip");
        if (!File.Exists(pgZip))
        {
            Console.WriteLine("[INSTALL] ERREUR : Fichier postgresql_portable.zip introuvable dans Resources/");
            return false;
        }

        try
        {
            var pgDir = Path.Combine(installDir, "Database", "PostgreSQL");
            Directory.CreateDirectory(pgDir);
            System.IO.Compression.ZipFile.ExtractToDirectory(pgZip, pgDir, overwriteFiles: true);

            // Initialize the database cluster
            var initdbPath = Path.Combine(pgDir, "bin", "initdb.exe");
            var dataDir = Path.Combine(installDir, "Database", "data");

            if (!Directory.Exists(dataDir))
            {
                RunProcess(initdbPath, $"-D \"{dataDir}\" -E UTF8 --locale=C -U skyra_admin");
                Console.WriteLine("[INSTALL] Cluster PostgreSQL initialisé avec succès.");
            }

            Console.WriteLine("[INSTALL] PostgreSQL Portable installé.");
            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[INSTALL] Erreur PostgreSQL : {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Installs wkhtmltopdf silently using the bundled installer.
    /// </summary>
    public static bool InstallWkhtmltopdf()
    {
        Console.WriteLine("[INSTALL] Installation de wkhtmltopdf en cours...");

        var installer = Path.Combine(ResourcesPath, "wkhtmltopdf_setup.exe");
        if (!File.Exists(installer))
        {
            Console.WriteLine("[INSTALL] AVERTISSEMENT : wkhtmltopdf_setup.exe introuvable. Ignoré.");
            return false;
        }

        // /S = silent mode for wkhtmltopdf installer
        bool success = RunProcess(installer, "/S");
        Console.WriteLine(success
            ? "[INSTALL] wkhtmltopdf installé avec succès."
            : "[INSTALL] Erreur lors de l'installation de wkhtmltopdf.");
        return success;
    }

    /// <summary>
    /// Starts the PostgreSQL service for SKYRA.
    /// </summary>
    public static bool StartPostgresPortable(string installDir)
    {
        var pgCtl = Path.Combine(installDir, "Database", "PostgreSQL", "bin", "pg_ctl.exe");
        var dataDir = Path.Combine(installDir, "Database", "data");
        var logFile = Path.Combine(installDir, "Database", "postgres.log");

        if (!File.Exists(pgCtl)) return false;

        Console.WriteLine("[INSTALL] Démarrage de PostgreSQL...");
        return RunProcess(pgCtl, $"start -D \"{dataDir}\" -l \"{logFile}\" -w");
    }

    private static bool RunProcess(string exe, string args)
    {
        try
        {
            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = exe,
                    Arguments = args,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                }
            };
            process.Start();
            process.WaitForExit(60_000); // maximum 60 seconds
            return process.ExitCode == 0;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[PROCESS] Erreur : {ex.Message}");
            return false;
        }
    }
}
