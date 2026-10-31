using System.Windows;
using Nakawari.Services;
using Nakawari.ViewModels;

namespace Nakawari;

public partial class MainWindow : Window
{
    public MainWindow(AppServices services)
    {
        InitializeComponent();
        var viewModel = new MainViewModel(services);
        DataContext = viewModel;
        SourceInitialized += (_, _) => ThemeService.SetDarkTitleBar(this, services.Theme.IsNight);
        Loaded += async (_, _) => await viewModel.InitializeAsync();
    }

    /// <summary>Buttons that carry a ContextMenu open it on a normal left click.</summary>
    private void OpenMenu(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement { ContextMenu: { } menu } element) return;
        menu.PlacementTarget = element;
        menu.Placement = System.Windows.Controls.Primitives.PlacementMode.Bottom;
        menu.IsOpen = true;
    }
}
