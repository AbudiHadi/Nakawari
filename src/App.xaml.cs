using System.Windows;
using Nakawari.Cli;
using Nakawari.Core;
using Nakawari.Services;

namespace Nakawari;

public partial class App : Application
{
    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        DispatcherUnhandledException += (_, args) =>
        {
            Log.Error("Unhandled UI error", args.Exception);
            MessageBox.Show(args.Exception.Message, AppInfo.Name, MessageBoxButton.OK, MessageBoxImage.Error);
            args.Handled = true;
        };

        LegacyMigration.RunOnce();

        if (CliRunner.Handles(e.Args))
        {
            ShutdownMode = ShutdownMode.OnExplicitShutdown;
            Shutdown(await CliRunner.RunAsync(e.Args));
            return;
        }

        var services = new AppServices();
        services.Theme.Apply();
        new MainWindow(services).Show();
    }
}
