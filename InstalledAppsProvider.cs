using Microsoft.Win32;

namespace NetRouteManager;

public static class InstalledAppsProvider
{
    private static readonly (RegistryKey Hive, string Path)[] Sources =
    [
        (Registry.LocalMachine, @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall"),
        (Registry.LocalMachine, @"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall"),
        (Registry.CurrentUser,  @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall"),
        (Registry.CurrentUser,  @"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall"),
    ];

    // Last diagnostic run — read by AppPickerDialog to show error details
    public static string LastDiagnostic { get; private set; } = "";

    public static List<InstalledApp> GetAll()
    {
        var log   = new System.Text.StringBuilder();
        var result = new List<InstalledApp>();

        int totalRaw = 0, skipNoName = 0, skipParent = 0, skipNoUninstall = 0, skipNoExe = 0;

        foreach (var (hive, path) in Sources)
        {
            int before = result.Count;
            ReadKey(hive, path, result, log,
                    ref totalRaw, ref skipNoName, ref skipParent, ref skipNoUninstall, ref skipNoExe);
            log.AppendLine($"[{hive.Name[..4]}\\{path.Split('\\').Last()}] +{result.Count - before}");
        }

        var final = result
            .DistinctBy(a => a.ExePath, StringComparer.OrdinalIgnoreCase)
            .OrderBy(a => a.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();

        log.AppendLine($"---");
        log.AppendLine($"Raw keys:         {totalRaw}");
        log.AppendLine($"Skip (no name):   {skipNoName}");
        log.AppendLine($"Skip (has parent):{skipParent}");
        log.AppendLine($"Skip (no uninst): {skipNoUninstall}");
        log.AppendLine($"Skip (no exe):    {skipNoExe}");
        log.AppendLine($"Before distinct:  {result.Count}");
        log.AppendLine($"Final result:     {final.Count}");

        LastDiagnostic = log.ToString();

        // Write to temp file so user/dev can inspect it
        try
        {
            string tmp = Path.Combine(Path.GetTempPath(), "netroute_apps.txt");
            File.WriteAllText(tmp, LastDiagnostic);
        }
        catch { }

        return final;
    }

    private static void ReadKey(RegistryKey hive, string subPath, List<InstalledApp> result,
                                 System.Text.StringBuilder log,
                                 ref int totalRaw, ref int skipNoName, ref int skipParent,
                                 ref int skipNoUninstall, ref int skipNoExe)
    {
        try
        {
            using var key = hive.OpenSubKey(subPath);
            if (key is null)
            {
                log.AppendLine($"  WARN: could not open {hive.Name[..4]}\\{subPath}");
                return;
            }

            foreach (var name in key.GetSubKeyNames())
            {
                try
                {
                    totalRaw++;
                    using var sub = key.OpenSubKey(name);
                    if (sub is null) continue;

                    string? displayName = sub.GetValue("DisplayName") as string;
                    if (string.IsNullOrWhiteSpace(displayName)) { skipNoName++; continue; }

                    if (sub.GetValue("ParentKeyName") is string) { skipParent++; continue; }

                    bool hasUninstall = sub.GetValue("UninstallString") is string u
                                        && !string.IsNullOrWhiteSpace(u);
                    if (!hasUninstall) { skipNoUninstall++; continue; }

                    string? exePath = ResolveExePath(sub);
                    if (exePath is null) { skipNoExe++; continue; }

                    result.Add(new InstalledApp(displayName.Trim(), exePath));
                }
                catch (Exception ex)
                {
                    log.AppendLine($"  ERR key [{name}]: {ex.Message}");
                }
            }
        }
        catch (Exception ex)
        {
            log.AppendLine($"  ERR opening [{subPath}]: {ex.Message}");
        }
    }

    private static string? ResolveExePath(RegistryKey sub)
    {
        if (sub.GetValue("DisplayIcon") is string icon)
        {
            string path = icon.Split(',')[0].Trim().Trim('"').Trim();
            if (TryResolve(path, out string? full)) return full;
        }

        if (sub.GetValue("InstallLocation") is string location
            && !string.IsNullOrWhiteSpace(location))
        {
            string dir = location.Trim().Trim('"');
            if (Directory.Exists(dir))
            {
                string nameLower = Path.GetFileNameWithoutExtension(
                    sub.GetValue("DisplayName") as string ?? "").ToLower();

                var exes = Directory.GetFiles(dir, "*.exe", SearchOption.TopDirectoryOnly);

                if (exes.Length == 1 && TryResolve(exes[0], out string? only))
                    return only;

                if (exes.Length > 1)
                {
                    foreach (var candidate in exes.OrderBy(e =>
                    {
                        string stem = Path.GetFileNameWithoutExtension(e).ToLower();
                        return stem == nameLower ? 0 : stem.Contains(nameLower) ? 1 : 2;
                    }))
                    {
                        if (TryResolve(candidate, out string? match)) return match;
                    }
                }
            }
        }

        return null;
    }

    private static bool TryResolve(string path, [System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out string? result)
    {
        result = null;
        if (string.IsNullOrWhiteSpace(path)) return false;
        if (!path.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)) return false;
        try
        {
            string full = Path.GetFullPath(path);
            if (File.Exists(full)) { result = full; return true; }
            if (File.Exists(path)) { result = path; return true; }
        }
        catch { }
        return false;
    }
}
