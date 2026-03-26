using System.Diagnostics;
using ERP.Bootstrapper;

// ╔══════════════════════════════════════════════════════════════╗
// ║   SKYRA ERP — Bootstrapper (Orchestrateur d'installation)   ║
// ║   Ce programme gère l'installation complète de l'ERP        ║
// ║   sans aucune intervention manuelle du client.              ║
// ╚══════════════════════════════════════════════════════════════╝

const string AppName = "SKYRA";
string InstallDir = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..")); // Monte d'un niveau (Installer -> SKYRA)
const string WatchdogServiceName = "SKYRA_Watchdog";

try
{
    Console.Title = $"{AppName} ERP — Installation en cours...";
    Console.ForegroundColor = ConsoleColor.Cyan;
    Console.WriteLine("╔═══════════════════════════════════════╗");
    Console.WriteLine($"║   Bienvenue dans l'installateur {AppName}   ║");
    Console.WriteLine("╚═══════════════════════════════════════╝");
    Console.ResetColor();
    Console.WriteLine();

    // ── PHASE 0 : Téléchargement des ressources (Optionnel si manquant) ──
    Console.ForegroundColor = ConsoleColor.Cyan;
    Console.WriteLine("╔═══════════════════════════════════════╗");
    Console.WriteLine("║   Vérification des Ressources Distantes ║");
    Console.WriteLine("╚═══════════════════════════════════════╝");
    Console.ResetColor();

    string pgZip = Path.Combine(InstallDir, "Database", "postgresql_portable.zip");
    string erpZip = Path.Combine(InstallDir, "Database", "erp_binaries.zip");

    // URLs à mettre à jour par l'utilisateur (Final)
    string pgUrl = "https://github.com/fethi17891789/erp_pfc_20252026/releases/download/V1.0.0/postgresql_portable.zip";
    string erpUrl = "https://github.com/fethi17891789/erp_pfc_20252026/releases/download/V1.0.0/erp_binaries.zip";

    if (!File.Exists(pgZip))
    {
        Console.WriteLine("[INFO] Ressources PostgreSQL manquantes. Téléchargement...");
        var success = await WebDownloader.DownloadFileAsync(pgUrl, pgZip);
        if (!success) throw new Exception("Impossible de télécharger PostgreSQL.");
    }

    if (!File.Exists(erpZip))
    {
        Console.WriteLine("[INFO] Binaires ERP manquants. Téléchargement...");
        var success = await WebDownloader.DownloadFileAsync(erpUrl, erpZip);
        if (!success) throw new Exception("Impossible de télécharger les binaires ERP.");
    }

    // ── PHASE 1 : Détection du port ───────────────────────────
    Console.ForegroundColor = ConsoleColor.Yellow;
    Console.WriteLine("► Étape 1/5 : Détection du port réseau disponible...");
    Console.ResetColor();

    int selectedPort = PortScanner.FindAvailablePort();
    Console.WriteLine($"[PORT] Utilisation du port : {selectedPort}");

    // ── PHASE 2 : Vérification des dépendances système ──────────────────
    Console.ForegroundColor = ConsoleColor.Yellow;
    Console.WriteLine("\n► Étape 2/5 : Vérification des dépendances système...");
    Console.ResetColor();

    if (!Directory.Exists(InstallDir)) Directory.CreateDirectory(InstallDir);

    if (!DependencyChecker.IsVCRedistInstalled())
    {
        SilentInstaller.InstallVCRedist();
    }

    if (!DependencyChecker.IsPostgresInstalled())
    {
        // ── Étape 1 : Préparation de PostgreSQL ──
        if (!SilentInstaller.InstallPostgresPortable(InstallDir))
            throw new Exception("Échec de l'installation de PostgreSQL.");
    }

    // ── Étape 1.5 : Préparation des binaires de l'ERP ──
    if (!SilentInstaller.InstallErpBinaries(InstallDir))
        throw new Exception("Échec de l'extraction des binaires de l'ERP.");

    if (!DependencyChecker.IsWkhtmltopdfInstalled())
        SilentInstaller.InstallWkhtmltopdf();

    // Démarrage de PostgreSQL portable
    SilentInstaller.StartPostgresPortable(InstallDir);

    // ── PHASE 3 : Configuration du pare-feu ─────────────────────────
    Console.ForegroundColor = ConsoleColor.Yellow;
    Console.WriteLine("\n► Étape 3/5 : Configuration du pare-feu Windows...");
    Console.ResetColor();

    FirewallConfigurator.OpenPort(selectedPort);

    // ── PHASE 4 : Configuration de l'ERP ──────────────────────
    Console.ForegroundColor = ConsoleColor.Yellow;
    Console.WriteLine("\n► Étape 4/5 : Configuration de l'ERP SKYRA...");
    Console.ResetColor();
    string erpDir = Path.Combine(InstallDir, "ERP");
    if (!Directory.Exists(erpDir)) Directory.CreateDirectory(erpDir);

    string appSettingsFile = Path.Combine(erpDir, "appsettings.Production.json");
    string erpConfigFile = Path.Combine(erpDir, "erpconfig.json");

    // On ne crée les fichiers de config que s'ils n'existent pas déjà 
    // (Pour ne pas écraser les réglages de l'utilisateur lors d'une ré-installation)
    if (!File.Exists(appSettingsFile))
    {
        string appSettingsContent = "{\n  \"Kestrel\": {\n    \"Endpoints\": {\n      \"Http\": {\n        \"Url\": \"http://localhost:" + selectedPort + "\"\n      }\n    }\n  }\n}";
        await File.WriteAllTextAsync(appSettingsFile, appSettingsContent);
        Console.WriteLine($"[CONFIG] Configuration ASP.NET créée : {appSettingsFile}");
    }

    if (!File.Exists(erpConfigFile))
    {
        // Accès Superuser par défaut pour le premier lancement
        var connString = $"Host=localhost;Port=5432;Database=postgres;Username=postgres;Password=";
        string erpConfigContent = "{\n  \"ConnectionStrings\": {\n    \"DefaultConnection\": \"" + connString + "\"\n  }\n}";
        await File.WriteAllTextAsync(erpConfigFile, erpConfigContent);
        Console.WriteLine($"[CONFIG] Configuration de connexion créée : {erpConfigFile}");
    }

    // ── PHASE 5 : Attendre que l'ERP soit prêt ──────────────────────
    Console.ForegroundColor = ConsoleColor.Yellow;
    Console.WriteLine("\n► Étape 5/5 : Attente du démarrage de SKYRA ERP...");
    Console.ResetColor();

    // 1ère tentative : On attend que le service Watchdog lance l'ERP (30s)
    bool isReady = await PortScanner.WaitForPortReadyAsync(selectedPort, TimeSpan.FromSeconds(30));

    if (!isReady)
    {
        Console.WriteLine("[INFO] L'ERP ne semble pas démarré par le service. Tentative de lancement manuel...");
        string erpExe = Path.Combine(InstallDir, "ERP", "SkyraERP.exe");
        if (File.Exists(erpExe))
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = erpExe,
                WorkingDirectory = Path.Combine(InstallDir, "ERP"),
                UseShellExecute = true
            });
            // 2ème tentative : On redonne 30 secondes pour le démarrage manuel
            isReady = await PortScanner.WaitForPortReadyAsync(selectedPort, TimeSpan.FromSeconds(30));
        }
    }

    if (isReady)
    {
        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine("\n╔═══════════════════════════════════════════════════╗");
        Console.WriteLine($"║  SKYRA ERP est opérationnel sur le port {selectedPort}!   ║");
        Console.WriteLine("║  Ouverture du navigateur...                       ║");
        Console.WriteLine("╚═══════════════════════════════════════════════════╝");
        Console.ResetColor();

        // Ouvrir le navigateur par défaut sur l'ERP
        Process.Start(new ProcessStartInfo
        {
            FileName = $"http://localhost:{selectedPort}",
            UseShellExecute = true
        });
    }
    else
    {
        Console.ForegroundColor = ConsoleColor.Red;
        Console.WriteLine("\n[ERREUR] Impossible de confirmer le démarrage de l'ERP.");
        Console.WriteLine($"Veuillez essayer d'ouvrir http://localhost:{selectedPort} manuellement.");
        Console.ResetColor();
    }

    Console.WriteLine("\n[FINAL] Installation terminée. Appuyez sur une touche pour quitter...");
    Console.ReadKey();
}
catch (Exception ex)
{
    Console.ForegroundColor = ConsoleColor.Red;
    Console.WriteLine("\n❌ UNE ERREUR CRITIQUE EST SURVENUE.");
    Console.WriteLine($"Message: {ex.Message}");
    if (ex.InnerException != null) Console.WriteLine($"Détail interne : {ex.InnerException.Message}");
    Console.WriteLine($"\nEmplacement de l'erreur :\n{ex.StackTrace}");

    try 
    {
        // Tentative de log
        string logDir = Path.Combine(InstallDir, "Logs");
        if (!Directory.Exists(logDir)) Directory.CreateDirectory(logDir);
        string logFile = Path.Combine(logDir, "installer_error.log");
        string errorMsg = $"[ERREUR] {DateTime.Now}\nMessage: {ex.Message}\nStackTrace: {ex.StackTrace}\nInner: {ex.InnerException?.Message}";
        File.WriteAllText(logFile, errorMsg);
        Console.WriteLine($"\n[LOGS] Détails enregistrés dans : {logFile}");
    }
    catch (Exception logEx) 
    {
        Console.WriteLine($"\n⚠️ Attention : Impossible de créer le fichier log ({logEx.Message})");
    }

    Console.ResetColor();
    Console.WriteLine("\n[BLOCAGE] La console restera ouverte pour vous laisser lire l'erreur.");
    Console.WriteLine("Appuyez sur n'importe quelle touche pour fermer...");
    
    while (!Console.KeyAvailable) 
    {
        System.Threading.Thread.Sleep(100);
    }
    Console.ReadKey(true);
}
