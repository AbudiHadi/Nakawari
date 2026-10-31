using Nakawari.Core;
using Nakawari.Models;

namespace Nakawari.Services.Kit;

/// <summary>
/// Puts the 50-series kit into one enabled game: replaces the game's whole Streamline set (every
/// sl.*.dll next to its sl.interposer.dll) and its DLSS-G with the kit's, backing each original up.
/// All-or-nothing: if the game uses a Streamline plugin the kit lacks, nothing is touched.
/// </summary>
public sealed class KitInstaller(NvidiaKit kit, LedgerStore ledgers)
{
    private const string Interposer = "sl.interposer.dll";

    private static readonly EnumerationOptions Walk = new()
    {
        RecurseSubdirectories = true, MaxRecursionDepth = 10, IgnoreInaccessible = true,
        AttributesToSkip = FileAttributes.ReparsePoint,
    };

    /// <summary>Folders holding the game's Streamline runtime.</summary>
    public static IReadOnlyList<string> StreamlineFolders(Ledger ledger)
    {
        var root = ledger.InstallDir ?? ledger.ExeDir;
        return Directory.Exists(root)
            ? Directory.EnumerateFiles(root, Interposer, Walk).Select(f => Path.GetDirectoryName(f)!).Distinct().ToList()
            : [];
    }

    public static bool HasStreamline(Ledger ledger) => StreamlineFolders(ledger).Count > 0;

    /// <summary>True when a game patch replaced some of the kit's files since it was installed.</summary>
    public bool NeedsRepair(Ledger ledger) =>
        ledger.KitVersion is not null && kit.IsReady &&
        ledger.Files.Any(f => f.Role == LedgerFile.Streamline && File.Exists(f.Path) && !kit.IsKitCopy(f.Path));

    /// <summary>Never throws: download or file problems come back as a failed result with a plain reason.</summary>
    public async Task<OpResult> InstallAsync(Ledger ledger)
    {
        try { return await InstallCoreAsync(ledger); }
        catch (Exception ex) when (GameFiles.IsExpected(ex))
        {
            Log.Error($"{ledger.GameName}: 50-series kit", ex);
            return OpResult.Fail($"{ledger.GameName}: kit not installed - {GameFiles.Explain(ex)}");
        }
    }

    private async Task<OpResult> InstallCoreAsync(Ledger ledger)
    {
        if (SystemInfo.IsRunning(ledger.ExePath)) return OpResult.Fail($"{ledger.GameName}: close the game first");
        var folders = StreamlineFolders(ledger);
        if (folders.Count == 0) return OpResult.Fail($"{ledger.GameName}: no Streamline runtime found, the kit can't apply");

        await kit.EnsureAsync();
        var plan = Plan(folders);
        if (plan.Missing.Count > 0)
            return OpResult.Fail($"{ledger.GameName}: uses Streamline plugins the kit doesn't have ({string.Join(", ", plan.Missing)}); left unchanged");

        foreach (var file in plan.Swaps)
        {
            var role = Path.GetFileName(file).Equals(DlssgService.FileName, StringComparison.OrdinalIgnoreCase)
                ? LedgerFile.Dlssg : LedgerFile.Streamline;
            if (!Pe.SameContent(file, kit.PathOf(Path.GetFileName(file)))) BackupVault.Preserve(file, role, ledger);
            SafeFs.Copy(kit.PathOf(Path.GetFileName(file)), file);
        }

        ledger.KitVersion = NvidiaKit.Version;
        ledger.Multiplier = MfgConfig.Dynamic;
        MfgConfig.ApplyMultiplier(ledger.ExeDir, ledger.Multiplier);
        ledger.Touch();
        ledgers.Save(ledger);
        return OpResult.Success($"{ledger.GameName}: 50-series kit on (Streamline {NvidiaKit.Version}, Dynamic MFG). " +
                                "Play once and Nakawari checks it worked.");
    }

    /// <summary>Puts the game's own Streamline and DLSS-G back; MFG itself stays enabled.</summary>
    public OpResult Remove(Ledger ledger)
    {
        if (SystemInfo.IsRunning(ledger.ExePath)) return OpResult.Fail($"{ledger.GameName}: close the game first");

        foreach (var file in ledger.Files.Where(f => f.Role is LedgerFile.Streamline or LedgerFile.Dlssg).ToList())
        {
            if (file.BackupPath is not null && File.Exists(file.BackupPath)) SafeFs.Copy(file.BackupPath, file.Path);
            ledger.Files.Remove(file);
        }

        ledger.KitVersion = null;
        if (ledger.Multiplier == MfgConfig.Dynamic) ledger.Multiplier = 0;
        MfgConfig.ApplyMultiplier(ledger.ExeDir, ledger.Multiplier);
        ledger.Touch();
        ledgers.Save(ledger);
        return OpResult.Success($"{ledger.GameName}: 50-series kit removed, the game's own Streamline is back");
    }

    private (List<string> Swaps, List<string> Missing) Plan(IReadOnlyList<string> folders)
    {
        var kitFiles = kit.FileNames.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var swaps = new List<string>();
        var missing = new List<string>();
        foreach (var folder in folders)
        {
            foreach (var file in Directory.EnumerateFiles(folder, "*.dll")
                         .Where(f => Path.GetFileName(f).StartsWith("sl.", StringComparison.OrdinalIgnoreCase)
                                     || Path.GetFileName(f).Equals(DlssgService.FileName, StringComparison.OrdinalIgnoreCase)))
            {
                if (kitFiles.Contains(Path.GetFileName(file))) swaps.Add(file);
                else missing.Add(Path.GetFileName(file));
            }
        }
        return (swaps, missing.Distinct(StringComparer.OrdinalIgnoreCase).ToList());
    }
}
