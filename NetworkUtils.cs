using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;

namespace NetRouteManager;

public record NetworkAdapter(string Name, string Status);

public static class NetworkUtils
{
    static NetworkUtils()
    {
        // .NET Core tidak include code page 850 by default
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
    }

    // ── Process helpers ───────────────────────────────────────────────────────

    private static string Run(string fileName, string arguments)
    {
        var psi = new ProcessStartInfo
        {
            FileName = fileName,
            Arguments = arguments,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            StandardOutputEncoding = Encoding.GetEncoding(850),
        };
        using var proc = Process.Start(psi)!;
        string output = proc.StandardOutput.ReadToEnd();
        proc.WaitForExit();
        return output;
    }

    private static (bool Ok, string Error) RunShell(string command)
    {
        var psi = new ProcessStartInfo
        {
            FileName = "cmd.exe",
            Arguments = $"/c {command}",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            StandardOutputEncoding = Encoding.GetEncoding(850),
        };
        using var proc = Process.Start(psi)!;
        string stdout = proc.StandardOutput.ReadToEnd();
        string stderr = proc.StandardError.ReadToEnd();
        proc.WaitForExit();

        return proc.ExitCode == 0
            ? (true, string.Empty)
            : (false, stderr.Trim().Length > 0 ? stderr.Trim() : stdout.Trim());
    }

    // ── Public API ────────────────────────────────────────────────────────────

    public static List<NetworkAdapter> GetAdapters()
    {
        var adapters = new List<NetworkAdapter>();
        string output = Run("netsh", "interface show interface");

        foreach (string line in output.Split('\n'))
        {
            string[] parts = line.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length >= 4 && parts[0] is "Enabled" or "Disabled")
            {
                string status = parts[1] == "Connected" ? "Connected" : "Disconnected";
                string name   = string.Join(" ", parts[3..]);
                adapters.Add(new NetworkAdapter(name, status));
            }
        }
        return adapters;
    }

    public static Dictionary<string, int> GetInterfaceMetrics()
    {
        var metrics = new Dictionary<string, int>();
        string output = Run("netsh", "interface ipv4 show interfaces");

        foreach (string line in output.Split('\n'))
        {
            string[] parts = line.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
            // Columns: Idx  Met  MTU  State  Name...
            if (parts.Length >= 5
                && int.TryParse(parts[0], out _)
                && int.TryParse(parts[1], out int metric))
            {
                string name = string.Join(" ", parts[4..]);
                metrics[name] = metric;
            }
        }
        return metrics;
    }

    public static (bool Ok, string Error) SetInterfaceMetric(string interfaceName, int metric)
        => RunShell($"netsh interface ipv4 set interface \"{interfaceName}\" metric={metric} store=active");

    public static Process? LaunchApp(string exePath, string arguments = "")
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = exePath,
                UseShellExecute = true, // lets Windows find the exe normally
            };
            if (!string.IsNullOrWhiteSpace(arguments))
                psi.Arguments = arguments;

            return Process.Start(psi);
        }
        catch
        {
            return null;
        }
    }
}
