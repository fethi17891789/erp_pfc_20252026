using System.Diagnostics;

namespace ERP.Bootstrapper;

/// <summary>
/// Configures Windows Firewall to allow traffic on the selected port.
/// Runs silently using netsh commands — no user interaction required.
/// </summary>
public static class FirewallConfigurator
{
    public static void OpenPort(int port)
    {
        Console.WriteLine($"[FIREWALL] Ouverture du port {port} dans le pare-feu Windows...");

        // Remove any existing SKYRA rule first to avoid duplicates
        RunNetsh($"advfirewall firewall delete rule name=\"SKYRA ERP\" protocol=TCP localport={port}");

        // Add inbound rule
        bool inbound = RunNetsh(
            $"advfirewall firewall add rule name=\"SKYRA ERP\" dir=in action=allow protocol=TCP localport={port} enable=yes profile=any");

        // Add outbound rule
        bool outbound = RunNetsh(
            $"advfirewall firewall add rule name=\"SKYRA ERP\" dir=out action=allow protocol=TCP localport={port} enable=yes profile=any");

        if (inbound && outbound)
            Console.WriteLine($"[FIREWALL] Port {port} ouvert avec succès.");
        else
            Console.WriteLine($"[FIREWALL] Avertissement : règle de pare-feu partiellement configurée.");
    }

    private static bool RunNetsh(string arguments)
    {
        try
        {
            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "netsh",
                    Arguments = arguments,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                }
            };
            process.Start();
            process.WaitForExit();
            return process.ExitCode == 0;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[FIREWALL] Erreur netsh: {ex.Message}");
            return false;
        }
    }
}
