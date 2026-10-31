using System.Text.RegularExpressions;
using Nakawari.Models;

namespace Nakawari.Services.Doctor;

/// <summary>
/// Reads ReShade.log after a play session. ReShade rewrites the log on every launch and several
/// processes can write into it, so only the last block written by the process that registered
/// the "MFG Unlock" addon is trusted. Unknown wording maps to Unknown, never to Problem.
/// </summary>
public static partial class ReShadeLogReader
{
    private const string AddonRegistered = "Registered add-on \"MFG Unlock\"";
    private const string InstallerPlaceholder = "it likely means ReShade was not loaded";

    [GeneratedRegex(@"^\S+ \[(\d+)\] \|")]
    private static partial Regex LinePid();

    [GeneratedRegex(@"rewrote (\d+) arch gate\(s\).* in (.+?nvngx_dlssg\.dll)", RegexOptions.IgnoreCase)]
    private static partial Regex ArchGatesRewritten();

    [GeneratedRegex(@"found 0 arch-gate comparisons in (.+?nvngx_dlssg\.dll)", RegexOptions.IgnoreCase)]
    private static partial Regex ArchGatesMissing();

    [GeneratedRegex(@"effective maximum (\d)x")]
    private static partial Regex EffectiveMaximum();

    [GeneratedRegex(@"slDLSSGGetState runtime status is 0x0 \(OK\)")]
    private static partial Regex RuntimeOk();

    public static GameHealth Read(string exeDir, DateTime changedAt)
    {
        var path = Path.Combine(exeDir, "ReShade.log");
        if (!File.Exists(path)) return GameHealth.NotRunYet;

        var seenAt = File.GetLastWriteTime(path);
        string[] lines;
        try { lines = File.ReadAllLines(path); }
        catch (IOException) { return GameHealth.NotRunYet with { Detail = "The game is running; check again after you close it." }; }

        if (lines.Any(l => l.Contains(InstallerPlaceholder)))
            return GameHealth.NotRunYet with { Detail = "Not played yet. If you already played, ReShade isn't starting with this game." };
        if (seenAt < changedAt) return GameHealth.NotRunYet;

        var registered = Array.FindLastIndex(lines, l => l.Contains(AddonRegistered));
        if (registered < 0)
        {
            return new GameHealth(HealthState.Problem, null,
                "ReShade started but the MFG addon didn't load. This ReShade build may lack add-on support.", seenAt);
        }

        return ReadAddonBlock(lines, registered, seenAt);
    }

    private static GameHealth ReadAddonBlock(string[] lines, int registered, DateTime seenAt)
    {
        var pid = LinePid().Match(lines[registered]).Groups[1].Value;
        var block = lines.Skip(registered).Where(l => LinePid().Match(l).Groups[1].Value == pid).ToList();

        var max = block.Select(l => EffectiveMaximum().Match(l)).LastOrDefault(m => m.Success) is { } m
            ? int.Parse(m.Groups[1].Value) : (int?)null;
        var unlocked = block.Any(l => ArchGatesRewritten().Match(l) is { Success: true } g && !IsDriverCopy(g.Groups[2].Value));
        var gameDllRefused = block.Any(l => ArchGatesMissing().Match(l) is { Success: true } g && !IsDriverCopy(g.Groups[1].Value));
        var runtimeOk = block.Any(l => RuntimeOk().IsMatch(l));

        if (unlocked || runtimeOk)
            return new GameHealth(HealthState.Working, max, "Frame generation unlocked in the last session.", seenAt);
        if (gameDllRefused)
            return new GameHealth(HealthState.Problem, max, "The addon doesn't recognise this game's DLSS-G version. Try Upgrade old DLSS-G.", seenAt);
        return new GameHealth(HealthState.Unknown, max, "The addon loaded; its log wording is new to this version of Nakawari.", seenAt);
    }

    /// <summary>The driver's own DLSS-G copy is often left alone on purpose; only the game's copy matters.</summary>
    private static bool IsDriverCopy(string path) => path.Contains(@"\DriverStore\", StringComparison.OrdinalIgnoreCase);
}
