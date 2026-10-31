namespace Nakawari.Core;

/// <summary>
/// Every folder the app owns. Nothing lives beside the exe (an installer may replace that folder on update)
/// and nothing lives inside a game folder.
/// </summary>
public static class AppPaths
{
    private static readonly string LocalAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);

    public static string Root { get; } = Ensure(Path.Combine(LocalAppData, AppInfo.Publisher, AppInfo.Name));

    /// <summary>Where the app kept its data while it was called MFG Unlocker.</summary>
    public static string LegacyRoot { get; } = Path.Combine(LocalAppData, AppInfo.Publisher, "MFGUnlocker");

    public static string AddonCache => Ensure(Path.Combine(Root, "cache", "addon"));
    public static string ReShadeCache => Ensure(Path.Combine(Root, "cache", "reshade"));
    public static string DlssgCache => Ensure(Path.Combine(Root, "cache", "dlssg"));
    public static string Ledgers => Ensure(Path.Combine(Root, "games"));
    public static string Backups => Ensure(Path.Combine(Root, "backups"));
    public static string SettingsFile => Path.Combine(Root, "settings.json");
    public static string LogFile => Path.Combine(Root, "log.txt");
    public static string MigrationMarker => Path.Combine(Root, ".migrated-from-mfgunlocker");

    private static string Ensure(string path)
    {
        Directory.CreateDirectory(path);
        return path;
    }
}
