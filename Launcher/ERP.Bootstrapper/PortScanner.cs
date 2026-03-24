using System.Net;
using System.Net.NetworkInformation;

namespace ERP.Bootstrapper;

/// <summary>
/// Scans for available network ports to avoid conflicts with other software.
/// Tries ports 5000, 5001, 5002... and returns the first free one.
/// </summary>
public static class PortScanner
{
    private static readonly int[] CandidatePorts = { 5000, 5001, 5002, 5003, 5004, 8080 };

    public static int FindAvailablePort()
    {
        var usedPorts = GetUsedPorts();

        foreach (var port in CandidatePorts)
        {
            if (!usedPorts.Contains(port))
            {
                Console.WriteLine($"[PORT] Port {port} disponible — sélectionné pour SKYRA.");
                return port;
            }
            Console.WriteLine($"[PORT] Port {port} déjà utilisé par un autre logiciel, passage au suivant...");
        }

        // Fallback: let the OS pick any free port
        var listener = new System.Net.Sockets.TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        int fallback = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        Console.WriteLine($"[PORT] Port de secours assigné automatiquement : {fallback}");
        return fallback;
    }

    private static HashSet<int> GetUsedPorts()
    {
        var props = IPGlobalProperties.GetIPGlobalProperties();
        var used = new HashSet<int>();

        foreach (var ep in props.GetActiveTcpListeners())
            used.Add(ep.Port);

        foreach (var ep in props.GetActiveUdpListeners())
            used.Add(ep.Port);

        return used;
    }
}
