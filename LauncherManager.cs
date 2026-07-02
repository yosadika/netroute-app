using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text.Json;

namespace NetRouteManager;

public class LauncherManager
{
    private static readonly string DataFile = Path.Combine(
        AppDomain.CurrentDomain.BaseDirectory, "launchers.json");

    private readonly List<AppLauncher> _launchers = [];
    private readonly ConcurrentDictionary<int, ActiveBinding> _active = new();

    public LauncherManager() => Load();

    // ── CRUD ─────────────────────────────────────────────────────────────────

    public IReadOnlyList<AppLauncher> GetAll() => _launchers.AsReadOnly();

    public void Add(AppLauncher launcher)    { _launchers.Add(launcher); Save(); }
    public void Remove(AppLauncher launcher) { _launchers.Remove(launcher); Save(); }

    public void Update(AppLauncher old, AppLauncher updated)
    {
        int idx = _launchers.IndexOf(old);
        if (idx >= 0) _launchers[idx] = updated;
        Save();
    }

    // ── Active bindings ───────────────────────────────────────────────────────

    public IEnumerable<ActiveBinding> GetActiveBindings() => _active.Values;

    public void Release(int pid, Action<string>? onStatus = null)
    {
        if (_active.TryRemove(pid, out var binding))
            Task.Run(() => RestoreMetrics(binding.OriginalMetrics, onStatus, "Released. Adapter metrics restored."));
    }

    // ── Launch ────────────────────────────────────────────────────────────────

    public Task LaunchAsync(AppLauncher launcher, Action<string> onStatus)
    {
        return Task.Run(async () =>
        {
            // 1. Snapshot
            var original  = NetworkUtils.GetInterfaceMetrics();
            var toRestore = new Dictionary<string, int>();

            // 2. Set preferred adapter
            foreach (var (name, metric) in original)
            {
                if (name == launcher.AdapterName)
                {
                    if (metric != 1) { NetworkUtils.SetInterfaceMetric(name, 1); toRestore[name] = metric; }
                }
                else if (metric < 9000)
                {
                    NetworkUtils.SetInterfaceMetric(name, 9999); toRestore[name] = metric;
                }
            }
            onStatus($"Adapter '{launcher.AdapterName}' set as preferred.");

            // 3. Launch
            var proc = NetworkUtils.LaunchApp(launcher.ExePath, launcher.Arguments);
            if (proc is null)
            {
                onStatus("Launch failed: could not start process.");
                RestoreMetrics(toRestore, onStatus);
                return;
            }

            _active[proc.Id] = new ActiveBinding(launcher, proc.Id, DateTime.Now, toRestore, proc);
            onStatus($"'{launcher.Name}' launched (PID {proc.Id}). Adapter locked until app exits.");

            // 4. Wait for exit
            await Task.Run(() => proc.WaitForExit());

            // 5. Restore if not already released
            if (_active.TryRemove(proc.Id, out _))
                RestoreMetrics(toRestore, onStatus, $"'{launcher.Name}' exited. Adapter metrics restored.");
        });
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static void RestoreMetrics(Dictionary<string, int> toRestore,
                                       Action<string>? onStatus,
                                       string finalMsg = "Adapter metrics restored.")
    {
        foreach (var (name, metric) in toRestore)
            NetworkUtils.SetInterfaceMetric(name, metric);
        onStatus?.Invoke(finalMsg);
    }

    private void Load()
    {
        if (!File.Exists(DataFile)) return;
        try
        {
            var data = JsonSerializer.Deserialize<List<AppLauncher>>(File.ReadAllText(DataFile));
            if (data is not null) _launchers.AddRange(data);
        }
        catch { }
    }

    private void Save()
    {
        var json = JsonSerializer.Serialize(_launchers,
            new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(DataFile, json);
    }
}
