using Nakawari.Core;
using Nakawari.Services;
using Velopack;

namespace Nakawari;

public static class Program
{
    /// <summary>
    /// Velopack's hooks must run first: on install/update/uninstall it starts the exe with special
    /// arguments, handles them here, and exits before any window appears.
    /// </summary>
    [STAThread]
    public static int Main()
    {
        VelopackApp.Build()
            .OnBeforeUninstallFastCallback(_ =>
            {
                StartupRegistration.Set(false);
                Log.Info("Uninstalling: sign-in entry removed. Games stay as they are; use Disable first to restore them.");
            })
            .Run();

        var app = new App();
        app.InitializeComponent();
        return app.Run();
    }
}
