using System.Diagnostics;
using ERP.Bootstrapper;

// ╔══════════════════════════════════════════════════════════════╗
// ║   SKYRA ERP — Bootstrapper (Orchestrateur d'installation)   ║
// ║   Ce programme gère l'installation complète de l'ERP        ║
// ║   sans aucune intervention manuelle du client.              ║
// ╚══════════════════════════════════════════════════════════════╝

const string AppName = "SKYRA";
const string InstallDir = @"C:\Program Files\SKYRA";
const string WatchdogServiceName = "SKYRA_Watchdog";

Console.Title = $"{AppName} ERP — Installation en cours...";
Console.ForegroundColor = ConsoleColor.Cyan;
Console.WriteLine("╔═══════════════════════════════════════╗");
Console.WriteLine($"║   Bienvenue dans l'installateur {AppName}   ║");
Console.WriteLine("╚═══════════════════════════════════════╝");
Console.ResetColor();
Console.WriteLine();

// ── PHASE 1 : Anti-conflit de port ──────────────────────────────
Console.ForegroundColor = ConsoleColor.Yellow;
Console.WriteLine("► Étape 1/5 : Détection du port réseau disponible...");
Console.ResetColor();

int selectedPort = PortScanner.FindAvailablePort();

// ── PHASE 2 : Détection et installation des dépendances ─────────
Console.ForegroundColor = ConsoleColor.Yellow;
Console.WriteLine("\n► Étape 2/5 : Vérification des dépendances système...");
Console.ResetColor();

Directory.CreateDirectory(InstallDir);

if (!DependencyChecker.IsPostgresInstalled())
{
    bool pgOk = SilentInstaller.InstallPostgresPortable(InstallDir);
    if (!pgOk)
    {
        Console.ForegroundColor = ConsoleColor.Red;
        Console.WriteLine("[ERREUR] Impossible d'installer PostgreSQL. Veuillez contacter le support SKYRA.");
        Console.ResetColor();
        Environment.Exit(1);
    }
}

if (!DependencyChecker.IsWkhtmltopdfInstalled())
    SilentInstaller.InstallWkhtmltopdf();

// Démarrage de PostgreSQL portable
SilentInstaller.StartPostgresPortable(InstallDir);

// ── PHASE 3 : Configuration du pare-feu ─────────────────────────
Console.ForegroundColor = ConsoleColor.Yellow;
Console.WriteLine("\n► Étape 3/5 : Configuration du pare-feu Windows...");
Console.ResetColor();

FirewallConfigurator.OpenPort(selectedPort);

// ── PHASE 4 : Écriture de la configuration de l'ERP ─────────────
Console.ForegroundColor = ConsoleColor.Yellow;
Console.WriteLine("\n► Étape 4/5 : Configuration de l'ERP SKYRA...");
Console.ResetColor();

var configPath = Path.Combine(InstallDir, "appsettings.Production.json");
var config = $$"""
{
  "Urls": "http://0.0.0.0:{{selectedPort}}",
  "Kestrel": {
    "Endpoints": {
      "Http": {
        "Url": "http://0.0.0.0:{{selectedPort}}"
      }
    }
  },
  "ConnectionStrings": {
    "DefaultConnection": "Host=localhost;Port=5432;Database=skyra_db;Username=skyra_admin;Password=skyra_secure_2026"
  }
}
""";
await File.WriteAllTextAsync(configPath, config);
Console.WriteLine($"[CONFIG] Configuration enregistrée : {configPath}");

// ── PHASE 5 : Démarrage et ouverture dans le navigateur ──────────
Console.ForegroundColor = ConsoleColor.Yellow;
Console.WriteLine("\n► Étape 5/5 : Lancement de SKYRA ERP...");
Console.ResetColor();

// Attendre 2 secondes pour que PostgreSQL soit pleinement démarré
await Task.Delay(2000);

// Démarrer le serveur ERP ASP.NET
var erpExePath = Path.Combine(InstallDir, "erp_pfc_20252026.exe");
if (File.Exists(erpExePath))
{
    Process.Start(new ProcessStartInfo
    {
        FileName = erpExePath,
        WorkingDirectory = InstallDir,
        UseShellExecute = false
    });

    await Task.Delay(3000);
}

// Ouvrir le navigateur par défaut sur l'ERP
Process.Start(new ProcessStartInfo
{
    FileName = $"http://localhost:{selectedPort}",
    UseShellExecute = true
});

Console.ForegroundColor = ConsoleColor.Green;
Console.WriteLine("\n╔═══════════════════════════════════════════════════╗");
Console.WriteLine($"║  SKYRA ERP est opérationnel sur le port {selectedPort}!   ║");
Console.WriteLine("║  Votre navigateur s'ouvre automatiquement...      ║");
Console.WriteLine("╚═══════════════════════════════════════════════════╝");
Console.ResetColor();

await Task.Delay(3000);
