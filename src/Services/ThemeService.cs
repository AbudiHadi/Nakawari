using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using Microsoft.Win32;
using Nakawari.Models;

namespace Nakawari.Services;

/// <summary>
/// Day/Night look. "Auto" follows Windows' app mode and switches live when it changes.
/// Also flips the window's title bar to dark so the chrome matches the night painting.
/// </summary>
public sealed class ThemeService
{
    private const int DwmUseImmersiveDarkMode = 20;
    private ThemeChoice _choice;

    public ThemeService(ThemeChoice choice)
    {
        _choice = choice;
        SystemEvents.UserPreferenceChanged += (_, e) =>
        {
            if (e.Category == UserPreferenceCategory.General && _choice == ThemeChoice.Auto)
                Application.Current.Dispatcher.Invoke(Apply);
        };
    }

    public ThemeChoice Choice
    {
        get => _choice;
        set { _choice = value; Apply(); }
    }

    public bool IsNight => _choice switch
    {
        ThemeChoice.Night => true,
        ThemeChoice.Day => false,
        _ => !WindowsUsesLightApps(),
    };

    public void Apply()
    {
        var dictionaries = Application.Current.Resources.MergedDictionaries;
        var current = dictionaries.FirstOrDefault(d => d.Source?.OriginalString.Contains("/Themes/") == true);
        if (current is not null) dictionaries.Remove(current);
        dictionaries.Add(new ResourceDictionary { Source = new Uri($"/Themes/{(IsNight ? "Night" : "Day")}.xaml", UriKind.Relative) });

        foreach (Window window in Application.Current.Windows) SetDarkTitleBar(window, IsNight);
    }

    public static void SetDarkTitleBar(Window window, bool dark)
    {
        var handle = new WindowInteropHelper(window).Handle;
        if (handle == IntPtr.Zero) return;
        var value = dark ? 1 : 0;
        DwmSetWindowAttribute(handle, DwmUseImmersiveDarkMode, ref value, sizeof(int));
    }

    private static bool WindowsUsesLightApps() =>
        Registry.GetValue(@"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Themes\Personalize", "AppsUseLightTheme", 1)
            is not int light || light != 0;

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);
}
