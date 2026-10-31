using Nakawari.Core;
using Nakawari.Models;

namespace Nakawari.Services;

/// <summary>
/// Remembers which frame-gen-capable games Nakawari has already seen, so a newly installed one can
/// be announced (and optionally enabled). The very first scan only records; it never floods alerts.
/// </summary>
public sealed class NewGameWatch
{
    private static string FilePath => Path.Combine(AppPaths.Root, "known-games.json");

    public IReadOnlyList<(GameEntry Game, ProbeResult Probe)> TakeNew(IEnumerable<(GameEntry Game, ProbeResult Probe)> scan)
    {
        var candidates = scan
            .Where(r => r.Probe.HasFrameGen && r.Probe.ExePath is not null && r.Probe.AntiCheat is null)
            .ToList();

        var known = JsonStore.Load<HashSet<string>>(FilePath);
        var firstRun = known is null;
        known ??= [];

        var fresh = candidates.Where(r => !known.Contains(r.Game.Id)).ToList();
        foreach (var (game, _) in fresh) known.Add(game.Id);
        if (fresh.Count > 0 || firstRun) JsonStore.Save(FilePath, known);

        return firstRun ? [] : fresh;
    }
}
