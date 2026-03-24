using Microsoft.Win32;

namespace ERP.Bootstrapper;

/// <summary>
/// Scans the Windows Registry and PATH for required dependencies.
/// Returns true if the dependency is already installed.
/// </summary>
public static class DependencyChecker
{
    public static bool IsPostgresInstalled()
    {
        // Check Windows Registry
        var key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\PostgreSQL Global Development Group\PostgreSQL");
        if (key != null)
        {
            Console.WriteLine("[DEPS] PostgreSQL détecté dans le registre Windows.");
            return true;
        }

        // Check PATH as a fallback
        if (IsInPath("psql"))
        {
            Console.WriteLine("[DEPS] PostgreSQL détecté via la variable PATH.");
            return true;
        }

        // Check common install directories
        var commonPaths = new[]
        {
            @"C:\Program Files\PostgreSQL",
            @"C:\Program Files (x86)\PostgreSQL"
        };
        foreach (var path in commonPaths)
        {
            if (Directory.Exists(path))
            {
                Console.WriteLine($"[DEPS] PostgreSQL détecté dans {path}");
                return true;
            }
        }

        Console.WriteLine("[DEPS] PostgreSQL NON détecté — installation requise.");
        return false;
    }

    public static bool IsWkhtmltopdfInstalled()
    {
        var key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\wkhtmltopdf");
        if (key != null) return true;

        if (IsInPath("wkhtmltopdf"))
        {
            Console.WriteLine("[DEPS] wkhtmltopdf détecté dans le PATH.");
            return true;
        }

        var commonPath = @"C:\Program Files\wkhtmltopdf\bin\wkhtmltopdf.exe";
        if (File.Exists(commonPath))
        {
            Console.WriteLine($"[DEPS] wkhtmltopdf détecté dans {commonPath}");
            return true;
        }

        Console.WriteLine("[DEPS] wkhtmltopdf NON détecté — installation requise.");
        return false;
    }

    public static bool IsDotNetInstalled(string minVersion = "8.0")
    {
        try
        {
            var process = new System.Diagnostics.Process
            {
                StartInfo = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "dotnet",
                    Arguments = "--version",
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };
            process.Start();
            var output = process.StandardOutput.ReadToEnd().Trim();
            process.WaitForExit();

            if (Version.TryParse(output.Split('-')[0], out var installedVersion) &&
                Version.TryParse(minVersion, out var required) &&
                installedVersion >= required)
            {
                Console.WriteLine($"[DEPS] .NET {output} détecté — OK.");
                return true;
            }
        }
        catch { /* dotnet not found */ }

        Console.WriteLine($"[DEPS] .NET {minVersion}+ NON détecté — installation requise.");
        return false;
    }

    private static bool IsInPath(string executable)
    {
        var pathVar = Environment.GetEnvironmentVariable("PATH") ?? "";
        foreach (var dir in pathVar.Split(Path.PathSeparator))
        {
            if (File.Exists(Path.Combine(dir, executable + ".exe")) ||
                File.Exists(Path.Combine(dir, executable)))
                return true;
        }
        return false;
    }
}
