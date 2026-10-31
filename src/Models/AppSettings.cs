using Nakawari.Core;

namespace Nakawari.Models;

public enum ThemeChoice { Auto, Day, Night }

public sealed class AppSettings
{
    public string AddonRepo { get; set; } = "mavismmg/MFGAdaUnlock-RenoDx";
    public bool UpgradeDlssg { get; set; } = true;
    public bool ShowAllGames { get; set; }
    public bool AutoEnableNewGames { get; set; }
    public List<string> ManualFolders { get; set; } = [];
    public ThemeChoice Theme { get; set; } = ThemeChoice.Auto;

    public static AppSettings Load() => JsonStore.Load<AppSettings>(AppPaths.SettingsFile) ?? new AppSettings();
    public void Save() => JsonStore.Save(AppPaths.SettingsFile, this);
}
