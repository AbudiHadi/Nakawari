using Nakawari.Core;
using Nakawari.Models;

namespace Nakawari.Services;

/// <summary>Enable and disable MFG for one game, and change its multiplier. Every file placed is recorded in the ledger.</summary>
public sealed class MfgInstaller(GameFiles files, DlssgService dlssg, LedgerStore ledgers, AppSettings settings)
{
    public string? Blocker(ProbeResult probe)
    {
        if (probe.AntiCheat is { } antiCheat) return $"Blocked: {antiCheat} (online anti-cheat)";
        if (!probe.HasFrameGen) return "No DLSS Frame Generation in this game";
        if (probe.ExePath is null) return "Game EXE not found";
        if (SystemInfo.IsRunning(probe.ExePath)) return "Close the game first";
        return files.ReShade.Conflict(probe.ExeDir!) is { } other ? $"dxgi.dll belongs to another mod ({other})" : null;
    }

    public Task<OpResult> EnableAsync(GameEntry game, ProbeResult probe, int multiplier)
    {
        if (Blocker(probe) is { } reason) return Task.FromResult(OpResult.Fail($"{game.Name}: {reason}"));

        var ledger = ledgers.Load(game.Id) ?? new Ledger { GameId = game.Id, GameName = game.Name };
        ledger.ExePath = probe.ExePath!;
        ledger.InstallDir = game.InstallDir;
        ledger.Multiplier = multiplier;
        return files.GuardedAsync(game.Name, ledger, async () =>
        {
            await files.EnsureReShadeAsync(ledger);
            await files.InstallAddonAsync(ledger);
            if (settings.UpgradeDlssg && dlssg.IsNewerThan(probe.DlssgVersion)) dlssg.Upgrade(probe.DlssgFiles, ledger);
            MfgConfig.Apply(ledger.ExeDir, multiplier, probe.Profile);
            ledger.Touch();
            return OpResult.Success($"{game.Name}: MFG enabled (addon {ledger.AddonTag}){GameFiles.ScsKillerHint(ledger.ExeDir)}");
        });
    }

    public OpResult SetMultiplier(Ledger ledger, int multiplier, GameProfile profile)
    {
        if (SystemInfo.IsRunning(ledger.ExePath)) return OpResult.Fail($"{ledger.GameName}: close the game to change the multiplier");
        ledger.Multiplier = multiplier;
        MfgConfig.Apply(ledger.ExeDir, multiplier, profile);
        ledgers.Save(ledger);
        return OpResult.Success($"{ledger.GameName}: multiplier set to {(multiplier == 0 ? "game menu" : multiplier + "x")}");
    }

    public OpResult SetPacing(Ledger ledger, PacingPreset preset)
    {
        if (SystemInfo.IsRunning(ledger.ExePath)) return OpResult.Fail($"{ledger.GameName}: close the game to change pacing");
        ledger.Pacing = preset;
        MfgConfig.ApplyPacing(ledger.ExeDir, preset);
        ledgers.Save(ledger);
        return OpResult.Success($"{ledger.GameName}: pacing set to {Pacing.Label(preset)} (takes effect next launch)");
    }

    public OpResult Disable(Ledger ledger)
    {
        if (File.Exists(ledger.ExePath) && SystemInfo.IsRunning(ledger.ExePath))
            return OpResult.Fail($"{ledger.GameName}: close the game first");

        try
        {
            if (Directory.Exists(ledger.ExeDir)) RemoveFiles(ledger);
            ledgers.Delete(ledger.GameId);
            var backups = Path.Combine(AppPaths.Backups, ledger.GameId);
            if (Directory.Exists(backups)) Directory.Delete(backups, recursive: true);
            Log.Info($"Disabled {ledger.GameName}");
            return OpResult.Success($"{ledger.GameName}: MFG removed, original files restored");
        }
        catch (Exception ex) when (GameFiles.IsExpected(ex))
        {
            Log.Error($"Disable {ledger.GameName}", ex);
            return OpResult.Fail($"{ledger.GameName}: {GameFiles.Explain(ex)}");
        }
    }

    private static void RemoveFiles(Ledger ledger)
    {
        var placedReShade = ledger.Find(LedgerFile.ReShade) is not null;
        foreach (var file in ledger.Files)
        {
            if (file.BackupPath is not null && File.Exists(file.BackupPath)) SafeFs.Copy(file.BackupPath, file.Path);
            else SafeFs.Delete(file.Path);
        }

        if (placedReShade && ledger.CreatedIni)
        {
            foreach (var name in new[] { "ReShade.ini", "ReShadePreset.ini", "ReShade.log" })
                SafeFs.Delete(Path.Combine(ledger.ExeDir, name));
        }
        else
        {
            MfgConfig.Remove(ledger.ExeDir);
        }
    }
}
