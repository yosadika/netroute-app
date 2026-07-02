using System.Security.Principal;

namespace NetRouteManager;

static class Program
{
    [STAThread]
    static void Main()
    {
        if (!IsAdmin())
        {
            Elevate();
            return;
        }

        ApplicationConfiguration.Initialize();
        Application.Run(new MainForm());
    }

    private static bool IsAdmin()
    {
        using var id = WindowsIdentity.GetCurrent();
        return new WindowsPrincipal(id).IsInRole(WindowsBuiltInRole.Administrator);
    }

    private static void Elevate()
    {
        var psi = new System.Diagnostics.ProcessStartInfo
        {
            FileName        = Environment.ProcessPath ?? "",
            UseShellExecute = true,
            Verb            = "runas",
        };
        try { System.Diagnostics.Process.Start(psi); }
        catch { /* user cancelled UAC */ }
    }
}
