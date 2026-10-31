using System.Drawing;
using System.Windows.Forms;
using Nakawari.Core;

namespace Nakawari.Services;

/// <summary>Windows notification from the windowless sign-in run (shows as a toast on Windows 10/11).</summary>
public static class Notifier
{
    public static async Task ShowAsync(string title, string text)
    {
        using var icon = new NotifyIcon
        {
            Icon = Icon.ExtractAssociatedIcon(Environment.ProcessPath!) ?? SystemIcons.Application,
            Text = AppInfo.Name,
            Visible = true,
        };
        icon.ShowBalloonTip(8000, title, text, ToolTipIcon.Info);
        await Task.Delay(TimeSpan.FromSeconds(9));   // keep the tray icon alive while Windows shows the toast
        icon.Visible = false;
    }
}
