using Nakawari.Core;

namespace Nakawari.Services.Doctor;

/// <summary>
/// The highest multiplier a game's Streamline DLSS-G plugin allows, keyed by its sl.dlss_g.dll version.
/// Seeded with versions observed on real games, then learned from every ReShade.log that reports
/// "effective maximum Nx", so later games with the same Streamline are predicted before first launch.
/// </summary>
public sealed class CeilingBook
{
    private static readonly Dictionary<string, int> Seed = new()
    {
        ["2.7.30"] = 4,     // Luto (Unreal plugin): compiled ceiling 3 generated frames
        ["2.12.130"] = 6,   // CONTROL Resonant: compiled ceiling 5 generated frames
    };

    private static string FilePath => Path.Combine(AppPaths.Root, "ceilings.json");
    private readonly Dictionary<string, int> _known;
    private readonly object _gate = new();

    public CeilingBook()
    {
        _known = new Dictionary<string, int>(Seed);
        foreach (var (version, max) in JsonStore.Load<Dictionary<string, int>>(FilePath) ?? [])
            _known[version] = max;
    }

    public int? Predict(Version? streamline)
    {
        if (streamline is null) return null;
        lock (_gate) return _known.TryGetValue(Key(streamline), out var max) ? max : null;
    }

    public void Learn(Version? streamline, int max)
    {
        if (streamline is null) return;
        lock (_gate)
        {
            if (_known.TryGetValue(Key(streamline), out var known) && known == max) return;
            _known[Key(streamline)] = max;
            JsonStore.Save(FilePath, _known);
        }
        Log.Info($"Learned: Streamline {Key(streamline)} allows up to {max}x");
    }

    private static string Key(Version v) => $"{v.Major}.{v.Minor}.{v.Build}";
}
