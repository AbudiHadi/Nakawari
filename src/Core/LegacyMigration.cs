using Microsoft.Win32;
using Nakawari.Models;

namespace Nakawari.Core;

/// <summary>
/// One-time move of MFG Unlocker data into Nakawari's folder. Runs once, guarded by a marker file
/// (the new root always exists, so its presence says nothing). Ledger backup paths are rewritten so
/// Disable can still restore the originals after the move.
/// </summary>
public static class LegacyMigration
{
    private const string LegacyRunValue = "MFGUnlocker";

    public static void RunOnce()
    {
        if (File.Exists(AppPaths.MigrationMarker)) return;
        try
        {
            if (Directory.Exists(AppPaths.LegacyRoot)) CopyData();
            RemoveLegacyRunValue();
            File.WriteAllText(AppPaths.MigrationMarker, DateTime.Now.ToString("O"));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Log.Error("Migration from MFG Unlocker failed; will retry next start", ex);
        }
    }

    private static void CopyData()
    {
        foreach (var folder in new[] { "cache", "backups" })
            CopyTree(Path.Combine(AppPaths.LegacyRoot, folder), Path.Combine(AppPaths.Root, folder));

        var oldSettings = Path.Combine(AppPaths.LegacyRoot, "settings.json");
        if (File.Exists(oldSettings) && !File.Exists(AppPaths.SettingsFile)) File.Copy(oldSettings, AppPaths.SettingsFile);

        var oldLedgers = Path.Combine(AppPaths.LegacyRoot, "games");
        if (!Directory.Exists(oldLedgers)) return;
        foreach (var file in Directory.EnumerateFiles(oldLedgers, "*.json"))
        {
            var ledger = JsonStore.Load<Ledger>(file);
            if (ledger is null) continue;
            foreach (var entry in ledger.Files.Where(f => f.BackupPath is not null))
                entry.BackupPath = entry.BackupPath!.Replace(AppPaths.LegacyRoot, AppPaths.Root, StringComparison.OrdinalIgnoreCase);
            JsonStore.Save(Path.Combine(AppPaths.Ledgers, Path.GetFileName(file)), ledger);
        }
        Log.Info($"Migrated data from {AppPaths.LegacyRoot}");
    }

    private static void CopyTree(string source, string target)
    {
        if (!Directory.Exists(source)) return;
        foreach (var file in Directory.EnumerateFiles(source, "*", SearchOption.AllDirectories))
        {
            var destination = Path.Combine(target, Path.GetRelativePath(source, file));
            Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
            if (!File.Exists(destination)) File.Copy(file, destination);
        }
    }

    private static void RemoveLegacyRunValue()
    {
        using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run", writable: true);
        key?.DeleteValue(LegacyRunValue, throwOnMissingValue: false);
    }
}
