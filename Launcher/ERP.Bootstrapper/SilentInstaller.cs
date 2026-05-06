using System.Diagnostics;
using System.IO.Compression;

namespace ERP.Bootstrapper;

public static class SilentInstaller
{
    private static readonly string ResourcesPath = Path.Combine(
        AppContext.BaseDirectory, "Resources");

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
                using (var archive = ZipFile.OpenRead(pgZip))
                {
                    foreach (var entry in archive.Entries)
                    {
                        string destPath = Path.GetFullPath(Path.Combine(pgDir, entry.FullName));
                        if (string.IsNullOrEmpty(entry.Name))
                        {
                            Directory.CreateDirectory(destPath);
                            continue;
                        }
                        string? destDir = Path.GetDirectoryName(destPath);
                        if (destDir != null) Directory.CreateDirectory(destDir);
                        try { entry.ExtractToFile(destPath, overwrite: true); }
                        catch (IOException) { Console.WriteLine($"[AVERTISSEMENT] Fichier ignoré : {entry.FullName}"); }
                    }
                }
                Console.WriteLine("[INSTALL] ✅ Extraction terminée avec succès.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERREUR] ÉCHEC DE L'EXTRACTION ZIP : {ex.Message}");
                throw;
            }

            var allFiles = Directory.GetFiles(pgDir, "*.*", SearchOption.AllDirectories);
            Console.WriteLine($"[DEBUG] Nombre de fichiers extraits : {allFiles.Length}");

            string? initdbPath = FindFileInDirectory(pgDir, "initdb.exe");

            if (string.IsNullOrEmpty(initdbPath))
            {
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

            var dataDir = Path.Combine(installDir, "Database", "data");

            if (!Directory.Exists(dataDir))
            {
                Console.WriteLine("[INSTALL] Initialisation du cluster de données...");
                bool initOk = RunProcess(initdbPath, $"-D \"{dataDir}\" -E UTF8 --locale=C -U postgres --auth=trust");
                if (!initOk) throw new Exception("ÉCHEC de l'initialisation du cluster PostgreSQL (initdb).");
                Console.WriteLine("[INSTALL] Cluster PostgreSQL initialisé avec succès.");
            }

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

    public static bool InstallWkhtmltopdf()
    {
        Console.WriteLine("[INSTALL] Installation de wkhtmltopdf en cours...");

        var installer = Path.Combine(ResourcesPath, "wkhtmltopdf_setup.exe");
        if (!File.Exists(installer))
        {
            Console.WriteLine("[INSTALL] AVERTISSEMENT : wkhtmltopdf_setup.exe introuvable. Ignoré.");
            return false;
        }

        bool success = RunProcess(installer, "/S");
        Console.WriteLine(success
            ? "[INSTALL] wkhtmltopdf installé avec succès."
            : "[INSTALL] Erreur lors de l'installation de wkhtmltopdf.");
        return success;
    }

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

        var processes = Process.GetProcessesByName("postgres");
        if (processes.Length > 0) return true;

        Console.WriteLine("[INSTALL] Démarrage de PostgreSQL...");
        return RunProcess(pgCtl, $"start -D \"{dataDir}\" -l \"{logFile}\" -w");
    }

    public static void ConfigureDatabase(string installDir)
    {
        var pgDir = Path.Combine(installDir, "Database", "PostgreSQL");
        var psql = FindFileInDirectory(pgDir, "psql.exe");

        // Fallback : chercher psql dans le PATH système (PostgreSQL installé globalement)
        if (string.IsNullOrEmpty(psql))
        {
            var pathVar = Environment.GetEnvironmentVariable("PATH") ?? "";
            foreach (var dir in pathVar.Split(Path.PathSeparator))
            {
                var candidate = Path.Combine(dir, "psql.exe");
                if (File.Exists(candidate)) { psql = candidate; break; }
            }
        }

        if (string.IsNullOrEmpty(psql))
        {
            Console.WriteLine("[INSTALL] psql.exe introuvable — configuration DB ignorée.");
            return;
        }

        Console.WriteLine("[INSTALL] Initialisation du rôle 'openpg'...");
        RunProcess(psql, "-U postgres -d postgres -c \"CREATE ROLE openpg WITH LOGIN SUPERUSER PASSWORD 'openpgpwd';\"");
        RunProcess(psql, "-U postgres -d postgres -c \"CREATE DATABASE fethifethifethi OWNER openpg;\"");
        Console.WriteLine("[INSTALL] Rôle configuré.");
    }

    public static async Task WaitForPostgresReadyAsync(int timeoutSeconds = 60)
    {
        Console.WriteLine("[INSTALL] Attente que PostgreSQL soit prêt...");
        var deadline = DateTime.UtcNow.AddSeconds(timeoutSeconds);
        while (DateTime.UtcNow < deadline)
        {
            try
            {
                using var client = new System.Net.Sockets.TcpClient();
                await client.ConnectAsync("127.0.0.1", 5432);
                Console.WriteLine("[INSTALL] PostgreSQL est prêt.");
                return;
            }
            catch { await Task.Delay(1000); }
        }
        Console.WriteLine("[INSTALL] Avertissement : PostgreSQL n'a pas répondu dans le délai imparti.");
    }

    private static string? FindFileInDirectory(string dir, string fileName)
    {
        if (!Directory.Exists(dir)) return null;
        var files = Directory.GetFiles(dir, fileName, SearchOption.AllDirectories);
        return files.Length > 0 ? files[0] : null;
    }

    private static bool RunProcess(string exe, string args, int timeoutMs = 180_000)
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

    public static bool InstallVCRedist()
    {
        Console.WriteLine("[INSTALL] Installation du Runtime Visual C++ (cela peut prendre 2-3 minutes)... ");

        var installer = Path.Combine(ResourcesPath, "vc_redist.x64.exe");
        if (!File.Exists(installer))
        {
            Console.WriteLine("[AVERTISSEMENT] vc_redist.x64.exe introuvable dans Resources. Étape ignorée.");
            return false;
        }

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
            using (var archive = ZipFile.OpenRead(erpZip))
            {
                foreach (var entry in archive.Entries)
                {
                    string destPath = Path.GetFullPath(Path.Combine(tempExtractPath, entry.FullName));
                    if (string.IsNullOrEmpty(entry.Name))
                    {
                        Directory.CreateDirectory(destPath);
                        continue;
                    }
                    string? destDir = Path.GetDirectoryName(destPath);
                    if (destDir != null) Directory.CreateDirectory(destDir);
                    try { entry.ExtractToFile(destPath, overwrite: true); }
                    catch (IOException) { Console.WriteLine($"[AVERTISSEMENT] Fichier ignoré : {entry.FullName}"); }
                }
            }

            var depsFiles = Directory.GetFiles(tempExtractPath, "*.deps.json", SearchOption.AllDirectories);
            if (depsFiles.Length == 0) throw new Exception("Aucun projet .NET trouvé dans le ZIP !");

            string depsFile = depsFiles[0];
            string sourceDir = Path.GetDirectoryName(depsFile)!;
            string projectName = Path.GetFileNameWithoutExtension(depsFile).Replace(".deps", "");
            string realExeName = projectName + ".exe";

            Console.WriteLine($"[INSTALL] Projet détecté : {projectName}");

            if (!Directory.Exists(erpTargetDir)) Directory.CreateDirectory(erpTargetDir);

            Console.WriteLine($"[INSTALL] Copie des binaires vers : {erpTargetDir}");
            foreach (var file in Directory.GetFiles(sourceDir, "*", SearchOption.AllDirectories))
            {
                string relPath = Path.GetRelativePath(sourceDir, file);
                string destFile = Path.Combine(erpTargetDir, relPath);
                string? destSubDir = Path.GetDirectoryName(destFile);
                if (destSubDir != null && !Directory.Exists(destSubDir)) Directory.CreateDirectory(destSubDir);
                File.Copy(file, destFile, true);
            }

            string exeNameFile = Path.Combine(installDir, "erp_exe_name.txt");
            File.WriteAllText(exeNameFile, realExeName);
            Console.WriteLine($"[INSTALL] Nom de l'exécutable enregistré : {realExeName}");

            Directory.Delete(tempExtractPath, true);

            Console.WriteLine("[INSTALL] ✅ ERP installé avec succès.");
            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[ERREUR] ÉCHEC DE L'INSTALLATION ERP : {ex.Message}");
            Console.WriteLine($"[ERREUR] Type : {ex.GetType().FullName}");
            Console.WriteLine($"[ERREUR] Stack trace :\n{ex}");

            // Supprimer le ZIP pour forcer un re-téléchargement propre au prochain lancement
            try
            {
                if (File.Exists(erpZip))
                {
                    File.Delete(erpZip);
                    Console.WriteLine("[ERREUR] erp_binaries.zip supprimé pour permettre un retry propre.");
                }
            }
            catch { }

            // Nettoyer aussi le dossier d'extraction temporaire s'il est resté
            try
            {
                if (Directory.Exists(tempExtractPath))
                    Directory.Delete(tempExtractPath, true);
            }
            catch { }

            return false;
        }
    }
}
