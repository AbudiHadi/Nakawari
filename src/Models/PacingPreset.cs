namespace Nakawari.Models;

/// <summary>
/// Frame-pacing presets built only from settings the MFG Unlock README documents for users
/// (developer/research switches are never touched). RTX 40 has no hardware flip metering, so
/// pacing is where it differs most from a 50-series card.
/// </summary>
public enum PacingPreset { GameDefault, SmoothVrr, LowLatency, FreezeFix }

public static class Pacing
{
    /// <summary>The addon keys each preset writes; GameDefault writes the addon's own defaults back.</summary>
    public static IReadOnlyDictionary<string, string> KeysFor(PacingPreset preset) => preset switch
    {
        PacingPreset.SmoothVrr => Keys(vrrCap: true, reflex: 2, flipMeteringOff: false),
        PacingPreset.LowLatency => Keys(vrrCap: true, reflex: 3, flipMeteringOff: false),
        PacingPreset.FreezeFix => Keys(vrrCap: true, reflex: 0, flipMeteringOff: true),
        _ => Keys(vrrCap: false, reflex: 0, flipMeteringOff: false),
    };

    public static string Label(PacingPreset preset) => preset switch
    {
        PacingPreset.SmoothVrr => "Smooth (G-SYNC / VRR)",
        PacingPreset.LowLatency => "Low latency",
        PacingPreset.FreezeFix => "Fix 3x/4x freezes",
        _ => "Game default",
    };

    private static Dictionary<string, string> Keys(bool vrrCap, int reflex, bool flipMeteringOff) => new()
    {
        ["ReflexVrrHeadroomEnabled"] = vrrCap ? "1" : "0",
        ["ReflexVrrHeadroomBasisPoints"] = "100",   // 1.0% under refresh, the addon's documented default
        ["ReflexModeOverride"] = reflex.ToString(),  // 0 game, 2 On, 3 On + Boost
        ["ForceFlipMeteringOff"] = flipMeteringOff ? "1" : "0",
    };
}
