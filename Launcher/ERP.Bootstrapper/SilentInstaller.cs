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
            pgZip = Path.Combine(installDir, "Database", "postgresql_portable.zip");
        }
        var pgDir = Path.Combine(installDir, "Database", "PostgreSQL");

        try
        {
            if (!Directory.Exists(pgDir)) Directory.CreateDirectory(pgDir);

            if (!File.Exists(pgZip))
            {
                throw new Exception($"Fichier ZIP introuvable : {pgZip}");
            }
            long zipSize = new FileInfo(pgZip).Length;
            Console.WriteLine($"[DEBUG] Taille du ZIP détectée : {zipSize / (1024 * 1024)} Mo");

            try
            {
                Console.WriteLine($"[INSTALL] Extraction de PostgreSQL vers {pgDir}...");
                Console.WriteLine("[INSTALL] Cette opération est gourmande en ressources, veuillez patienter...");
                System.IO.Compression.ZipFile.ExtractToDirectory(pgZip, pgDir, overwriteFiles: true);
                Console.WriteLine("[INSTALL] ✅ Extraction terminée avec succès.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERREUR] ÉCHEC DE L'EXTRACTION ZIP : {ex.Message}");
                throw;
            }

            // ── RECHERCHE DE initdb.exe ──
            var allFiles = Directory.GetFiles(pgDir, "*.*", SearchOption.AllDirectories);
            Console.WriteLine($"[DEBUG] Nombre de fichiers extraits : {allFiles.Length}");

            // ── RECHERCHE AGRESSIVE DES BINAIRES ──
            string? initdbPath = FindFileInDirectory(pgDir, "initdb.exe");
            
            if (string.IsNullOrEmpty(initdbPath))
            {
                // Si on ne trouve pas initdb, on cherche n'importe quel binaire postgres pour deviner
                var postgresExe = FindFileInDirectory(pgDir, "postgres.exe");
                if (!string.IsNullOrEmpty(postgresExe))
                {
                     var binFolder = Path.GetDirectoryName(postgresExe)!;
                     initdbPath = Path.Combine(binFolder, "initdb.exe");
                }
            }

            if (string.IsNullOrEmpty(initdbPath) || !File.Exists(initdbPath))
            {
                var allExes = Directory.GetFiles(pgDir, "*.exe", SearchOption.AllDirectories);
                string foundFolders = string.Join("\n", allExes.Select(f => Path.GetDirectoryName(f)!).Distinct().Take(10).Select(d => "- " + Path.GetRelativePath(pgDir, d)));
                
                throw new Exception($"ERREUR CRITIQUE : 'initdb.exe' est introuvable.\n"
                    + $"Votre pack PostgreSQL semble incomplet ou mal structuré.\n"
                    + $"Dossiers contenant des exécutables trouvés :\n{foundFolders}");
            }
            
            var binDir = Path.GetDirectoryName(initdbPath)!;
            var dataDir = Path.Combine(installDir, "Database", "data");

            if (!Directory.Exists(dataDir))
            {
                Console.WriteLine("[INSTALL] Initialisation du cluster de données...");
                bool initOk = RunProcess(initdbPath, $"-D \"{dataDir}\" -E UTF8 --locale=C -U postgres --auth=trust");
                if (!initOk) throw new Exception("ÉCHEC de l'initialisation du cluster PostgreSQL (initdb).");
                Console.WriteLine("[INSTALL] Cluster PostgreSQL initialisé avec succès.");
            }

            // Démarrage temporaire pour configuration
            if (StartPostgresPortable(installDir))
            {
                ConfigureDatabase(installDir);
            }
            else
            {
                throw new Exception("Impossible de démarrer PostgreSQL pour la configuration initiale.");
            }
            
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
        var pgDir = Path.Combine(installDir, "Database", "PostgreSQL");
        var pgCtl = FindFileInDirectory(pgDir, "pg_ctl.exe");
        var dataDir = Path.Combine(installDir, "Database", "data");
        var logFile = Path.Combine(installDir, "Database", "postgres.log");

        if (string.IsNullOrEmpty(pgCtl)) 
        {
            Console.WriteLine("[ERREUR] pg_ctl.exe introuvable.");
            return false;
        }

        // On vérifie si Postgres tourne déjà
        var processes = Process.GetProcessesByName("postgres");
        if (processes.Length > 0) return true;

        Console.WriteLine("[INSTALL] Démarrage de PostgreSQL...");
        return RunProcess(pgCtl, $"start -D \"{dataDir}\" -l \"{logFile}\" -w");
    }

    /// <summary>
    /// Configures the database (user password and database creation).
    /// </summary>
    private static void ConfigureDatabase(string installDir)
    {
        var pgDir = Path.Combine(installDir, "Database", "PostgreSQL");
        var psql = FindFileInDirectory(pgDir, "psql.exe");
        
        if (string.IsNullOrEmpty(psql)) return;

        Console.WriteLine("[INSTALL] Initialisation du rôle 'openpg'...");

        // On crée l'utilisateur 'openpg' avec le mot de passe attendu par l'ERP
        RunProcess(psql, "-U postgres -d postgres -c \"CREATE ROLE openpg WITH LOGIN SUPERUSER PASSWORD 'openpgpwd';\"");
        RunProcess(psql, "-U postgres -d postgres -c \"CREATE DATABASE fethifethifethi OWNER openpg;\"");
        
        Console.WriteLine("[INSTALL] Rôle configuré.");
    }

    private static string? FindFileInDirectory(string dir, string fileName)
    {
        if (!Directory.Exists(dir)) return null;
        var files = Directory.GetFiles(dir, fileName, SearchOption.AllDirectories);
        return files.Length > 0 ? files[0] : null;
    }

    private static bool RunProcess(string exe, string args, int timeoutMs = 60_000)
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
                    RedirectStandardOutput = false,
                    RedirectStandardError = false
                }
            };
            process.Start();
            bool finished = process.WaitForExit(timeoutMs);
            
            if (!finished) 
            {
                Console.WriteLine($"[PROCESS] Timeout après {timeoutMs/1000}s pour : {Path.GetFileName(exe)}");
                return false; 
            }
            
            return process.ExitCode == 0;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[PROCESS] Erreur sur {Path.GetFileName(exe)} : {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Installs VC++ Redist silently.
    /// </summary>

    public static bool InstallVCRedist()
    {
        Console.WriteLine("[INSTALL] Installation du Runtime Visual C++ (cela peut prendre 2-3 minutes)... ");

        var installer = Path.Combine(ResourcesPath, "vc_redist.x64.exe");
        if (!File.Exists(installer))
        { 
            Console.WriteLine("[AVERTISSEMENT] vc_redist.x64.exe introuvable dans Resources. Étape ignorée.");
            return false; 
        }

        // On donne 5 minutes (300 000 ms) pour cette installation système critique
        return RunProcess(installer, "/install /quiet /norestart", 300_000);
    }

    public static bool InstallErpBinaries(string installDir)
    {
        Console.WriteLine("[INSTALL] Extraction des binaires de l'ERP...");

        var erpZip = Path.Combine(ResourcesPath, "erp_binaries.zip");
        if (!File.Exists(erpZip))
        {
            erpZip = Path.Combine(installDir, "Database", "erp_binaries.zip");
        }

        string erpTargetDir = Path.Combine(installDir, "ERP");
        string tempExtractPath = Path.Combine(installDir, "TempERP");

        try
        {
            if (!File.Exists(erpZip))
            {
                Console.WriteLine("[INSTALL] Aucun pack de binaires ERP trouvé. Ignoré.");
                return true;
            }

            if (Directory.Exists(tempExtractPath)) Directory.Delete(tempExtractPath, true);
            Directory.CreateDirectory(tempExtractPath);

            Console.WriteLine("[INSTALL] Extraction temporaire de l'ERP...");
            System.IO.Compression.ZipFile.ExtractToDirectory(erpZip, tempExtractPath, overwriteFiles: true);

            // Trouver le dossier contenant le .exe principal (chercher un .deps.json pour identifier le projet .NET)
            var depsFiles = Directory.GetFiles(tempExtractPath, "*.deps.json", SearchOption.AllDirectories);
            if (depsFiles.Length == 0) throw new Exception("Aucun projet .NET trouvé dans le ZIP !");

            // Le .deps.json nous donne le vrai nom du projet
            string depsFile = depsFiles[0];
            string sourceDir = Path.GetDirectoryName(depsFile)!;
            string projectName = Path.GetFileNameWithoutExtension(depsFile).Replace(".deps", "");
            string realExeName = projectName + ".exe";

            Console.WriteLine($"[INSTALL] Projet détecté : {projectName}");

            if (!Directory.Exists(erpTargetDir)) Directory.CreateDirectory(erpTargetDir);

            // Copier TOUS les fichiers tels quels (sans renommer)
            Console.WriteLine($"[INSTALL] Copie des binaires vers : {erpTargetDir}");
            foreach (var file in Directory.GetFiles(sourceDir, "*", SearchOption.AllDirectories))
            {
                string relPath = Path.GetRelativePath(sourceDir, file);
                string destFile = Path.Combine(erpTargetDir, relPath);
                string? destSubDir = Path.GetDirectoryName(destFile);
                if (destSubDir != null && !Directory.Exists(destSubDir)) Directory.CreateDirectory(destSubDir);
                File.Copy(file, destFile, true);
            }

            // Sauvegarder le vrai nom de l'EXE pour que le Bootstrapper et le Watchdog le retrouvent
            string exeNameFile = Path.Combine(installDir, "erp_exe_name.txt");
            File.WriteAllText(exeNameFile, realExeName);
            Console.WriteLine($"[INSTALL] Nom de l'exécutable enregistré : {realExeName}");

            // Nettoyage
            Directory.Delete(tempExtractPath, true);

            Console.WriteLine("[INSTALL] ✅ ERP installé avec succès.");
            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[ERREUR] ÉCHEC DE L'INSTALLATION ERP : {ex.Message}");
            return false;
        }
    }
}
