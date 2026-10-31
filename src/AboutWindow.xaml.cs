using System.Diagnostics;
using System.Windows;
using Nakawari.Core;
using Nakawari.Services;

namespace Nakawari;

public partial class AboutWindow : Window
{
    public AboutWindow(AppServices services)
    {
        InitializeComponent();
        VersionLine.Text = $"Version {AppInfo.Version}  ·  by {AppInfo.Publisher}";
        SourceInitialized += (_, _) => ThemeService.SetDarkTitleBar(this, services.Theme.IsNight);
    }

    private void OpenRepo(object sender, RoutedEventArgs e) =>
        Process.Start(new ProcessStartInfo(AppInfo.RepoUrl) { UseShellExecute = true });

    private void CloseWindow(object sender, RoutedEventArgs e) => Close();
}
