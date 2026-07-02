using System.Diagnostics;

namespace NetRouteManager;

public class ActiveBinding
{
    public AppLauncher          Launcher        { get; }
    public int                  Pid             { get; }
    public DateTime             StartedAt       { get; }
    public Dictionary<string, int> OriginalMetrics { get; }
    internal Process            Process         { get; }

    public ActiveBinding(AppLauncher launcher, int pid, DateTime startedAt,
                         Dictionary<string, int> originalMetrics, Process process)
    {
        Launcher        = launcher;
        Pid             = pid;
        StartedAt       = startedAt;
        OriginalMetrics = originalMetrics;
        Process         = process;
    }
}
