using Nakawari.Core;
using Nakawari.Models;
using Nakawari.Services.Doctor;

namespace Nakawari.Services;

/// <summary>
/// Keeps an enabled game healthy without the user doing anything: newest addon, repairs what went
/// missing (ReShade, settings), re-upgrades DLSS-G after a game patch, and rolls the addon back when
/// it crashed the game twice since the last change. With the 50-series kit installed, a crashing game
/// first loses the kit (the most experimental piece) before the addon is touched.
/// </summary>
public sealed class MfgMaintainer(
    GameFiles files, AddonService addon, DlssgService dlssg, Kit.KitInstaller kit, ProfileCatalog profiles, AppSettings settings)
{
    private const int CrashesBeforeRollback = 2;

    public Task<OpResult> UpdateAsync(Ledger ledger)
    {
        if (!File.Exists(ledger.ExePath)) return Task.FromResult(OpResult.Fail($"{ledger.GameName}: game not found, skipped"));
        if (SystemInfo.IsRunning(ledger.ExePath)) return Task.FromResult(OpResult.Fail($"{ledger.GameName}: running, skipped"));

        return files.GuardedAsync(ledger.GameName, ledger, async () =>
        {
            if (ledger.AddonTag is not null && CrashWatch.CountSince(ledger) >= CrashesBeforeRollback)
            {
                if (ledger.KitVersion is null) return RollBack(ledger);
                var removed = kit.Remove(ledger);
                return removed with { Message = removed.Message + " (it crashed twice with the kit)" };
            }

            var before = ledger.AddonTag;
            var addonChanged = await files.InstallAddonAsync(ledger, await TargetTagAsync(ledger));
            var repairs = await RepairAsync(ledger);
            var dlssgChanged = UpgradeDlssg(ledger);
            var changed = addonChanged || repairs.Count > 0 || dlssgChanged;
            if (changed) ledger.Touch();

            var what = before == ledger.AddonTag ? $"up to date ({ledger.AddonTag})" : $"updated {before} -> {ledger.AddonTag}";
            var fixedText = repairs.Count > 0 ? $", repaired {string.Join(" and ", repairs)}" : "";
            var pinned = ledger.PinnedTag is not null ? $" (held at {ledger.PinnedTag} after crashes)" : "";
            return OpResult.Success($"{ledger.GameName}: {what}{fixedText}{pinned}{(changed ? GameFiles.ScsKillerHint(ledger.ExeDir) : "")}");
        });
    }

    /// <summary>The build to run: the pinned one while the bad build is still the latest, else the latest.</summary>
    private async Task<string?> TargetTagAsync(Ledger ledger)
    {
        if (ledger.PinnedTag is null) return null;
        try
        {
            var latest = (await addon.LatestAsync()).Tag;
            if (latest != ledger.BadTag)
            {
                Log.Info($"{ledger.GameName}: a newer addon ({latest}) replaced the bad build {ledger.BadTag}; unpinning");
                ledger.PinnedTag = ledger.BadTag = null;
                return null;
            }
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or IOException)
        {
            // Offline: keep the pin.
        }
        return ledger.PinnedTag;
    }

    private async Task<List<string>> RepairAsync(Ledger ledger)
    {
        var repairs = new List<string>();
        if (await files.EnsureReShadeAsync(ledger)) repairs.Add("ReShade");
        if (kit.NeedsRepair(ledger) && (await kit.InstallAsync(ledger)).Ok) repairs.Add("the 50-series kit");
        if (!MfgConfig.HasSection(ledger.ExeDir))
        {
            MfgConfig.Apply(ledger.ExeDir, ledger.Multiplier, profiles.For(ledger.GameName));
            if (ledger.Pacing != PacingPreset.GameDefault) MfgConfig.ApplyPacing(ledger.ExeDir, ledger.Pacing);
            repairs.Add("settings");
        }
        return repairs;
    }

    private bool UpgradeDlssg(Ledger ledger)
    {
        if (!settings.UpgradeDlssg) return false;
        var stale = ledger.Files
            .Where(f => f.Role == LedgerFile.Dlssg && File.Exists(f.Path) && dlssg.IsNewerThan(Pe.FileVersion(f.Path)))
            .Select(f => f.Path)
            .ToList();
        if (stale.Count > 0) dlssg.Upgrade(stale, ledger);
        return stale.Count > 0;
    }

    private OpResult RollBack(Ledger ledger)
    {
        var current = ledger.AddonTag!;
        var older = addon.CachedTags().LastOrDefault(t => AddonService.TagOrder(t) < AddonService.TagOrder(current));
        if (older is null)
        {
            return OpResult.Fail($"{ledger.GameName}: crashed {CrashesBeforeRollback}+ times since addon {current}, " +
                                 "and no older build is cached to roll back to. Consider Disable for this game.");
        }

        SafeFs.Copy(AddonService.PathFor(older), Path.Combine(ledger.ExeDir, AddonService.FileName));
        ledger.BadTag = current;
        ledger.PinnedTag = older;
        ledger.AddonTag = older;
        ledger.Touch();
        return OpResult.Success($"{ledger.GameName}: crashed after addon {current}, rolled back to {older}. " +
                                "It moves forward again when a newer addon ships.");
    }
}
