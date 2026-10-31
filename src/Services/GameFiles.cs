using System.Text;
using Nakawari.Core;
using Nakawari.Models;

namespace Nakawari.Services;

/// <summary>
/// Puts Nakawari's pieces into a game folder (ReShade, the addon) and runs every folder operation
/// under one guard that logs, explains errors in plain words, and always saves the ledger.
/// Shared by MfgInstaller (enable/disable) and MfgMaintainer (update/repair).
/// </summary>
public sealed class GameFiles(AddonService addon, ReShadeService reShade, LedgerStore ledgers)
{
    private const string LegacyVersionFile = "mfgunlock-version.txt";

    public ReShadeService ReShade => reShade;

    /// <summary>Installs ReShade as dxgi.dll unless some ReShade already runs here. Returns true when it wrote.</summary>
    public async Task<bool> EnsureReShadeAsync(Ledger ledger)
    {
        if (reShade.FindInstalled(ledger.ExeDir) is not null) return false;

        var cached = await reShade.EnsureCachedAsync();
        ledger.CreatedIni = ledger.CreatedIni || !File.Exists(MfgConfig.IniPath(ledger.ExeDir));
        var target = Path.Combine(ledger.ExeDir, ReShadeService.ProxyName);
        SafeFs.Copy(cached, target);
        ledger.Track(target, LedgerFile.ReShade);
        return true;
    }

    /// <summary>Puts the given (or latest) addon build in place. Returns true when the file changed.</summary>
    public async Task<bool> InstallAddonAsync(Ledger ledger, string? tag = null)
    {
        var (resolvedTag, cached) = await addon.EnsureCachedAsync(tag);
        var target = Path.Combine(ledger.ExeDir, AddonService.FileName);
        if (!BackupVault.IsTracked(target, ledger)) BackupVault.Preserve(target, LedgerFile.Addon, ledger);

        var changed = !Pe.SameContent(cached, target);
        if (changed) SafeFs.Copy(cached, target);
        ledger.Track(target, LedgerFile.Addon);
        ledger.AddonTag = resolvedTag;
        SyncLegacyUpdater(ledger, resolvedTag);
        return changed;
    }

    /// <summary>
    /// Keeps a hand-made per-game updater script (mfgunlock-update.ps1) from re-downloading the same build.
    /// Its original version file is backed up first, so Disable hands that script its old state back.
    /// </summary>
    private static void SyncLegacyUpdater(Ledger ledger, string tag)
    {
        var legacy = Path.Combine(ledger.ExeDir, LegacyVersionFile);
        if (!File.Exists(legacy)) return;
        if (!BackupVault.IsTracked(legacy, ledger)) BackupVault.Preserve(legacy, LedgerFile.Legacy, ledger);
        SafeFs.WriteText(legacy, tag, new UTF8Encoding(false));
    }

    /// <summary>Runs one folder operation: logs the outcome, turns expected failures into a message, saves the ledger.</summary>
    public async Task<OpResult> GuardedAsync(string gameName, Ledger ledger, Func<Task<OpResult>> action)
    {
        try
        {
            var result = await action();
            Log.Info(result.Message);
            return result;
        }
        catch (Exception ex) when (IsExpected(ex))
        {
            Log.Error(gameName, ex);
            return OpResult.Fail($"{gameName}: {Explain(ex)}");
        }
        finally
        {
            if (ledger.Files.Count > 0) ledgers.Save(ledger);
        }
    }

    public static bool IsExpected(Exception ex) =>
        ex is IOException or UnauthorizedAccessException or HttpRequestException or InvalidOperationException
            or InvalidDataException or TaskCanceledException or System.Text.Json.JsonException;

    public static string Explain(Exception ex) => ex switch
    {
        UnauthorizedAccessException => $"access denied - run {AppInfo.Name} as administrator for this folder",
        HttpRequestException or TaskCanceledException => "download failed - check your internet connection",
        _ => ex.Message,
    };

    /// <summary>SCSKiller's watcher may disarm its recorder when other files in the folder change.</summary>
    public static string ScsKillerHint(string exeDir) =>
        ScsKillerBridge.StateOf(exeDir) == ScsState.NotUsed ? "" : ". If SCSKiller shows it as not armed, open SCSKiller once";
}
