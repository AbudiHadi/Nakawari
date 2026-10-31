using Nakawari.Core;
using Nakawari.Models;

namespace Nakawari.Services;

/// <summary>
/// Reads and writes the addon's own settings in ReShade.ini ([RenoDX.MFGUnlock]).
/// Key names and ranges match the addon source: ForceMultiplier is 0 (game decides) or 2..6,
/// HDRCompatibilityMode is 0..3.
/// </summary>
public static class MfgConfig
{
    public const string Section = "RenoDX.MFGUnlock";
    public static readonly int[] Multipliers = [0, 2, 3, 4, 5, 6];

    /// <summary>Multiplier value meaning "NVIDIA Dynamic MFG" (needs the 50-series kit).</summary>
    public const int Dynamic = -1;

    public static string IniPath(string exeDir) => Path.Combine(exeDir, "ReShade.ini");

    public static int ReadMultiplier(string exeDir)
    {
        var ini = IniFile.Open(IniPath(exeDir));
        if (ini.Get(Section, "DynamicMFG") == "1") return Dynamic;
        return int.TryParse(ini.Get(Section, "ForceMultiplier"), out var value) && Multipliers.Contains(value) ? value : 0;
    }

    public static bool HasSection(string exeDir) =>
        IniFile.Open(IniPath(exeDir)).Get(Section, "Enabled") is not null;

    public static void Apply(string exeDir, int multiplier, GameProfile profile)
    {
        var ini = IniFile.Open(IniPath(exeDir));
        SetMultiplier(ini, multiplier);
        if (profile.HdrMode is { } hdr) ini.Set(Section, "HDRCompatibilityMode", hdr.ToString());
        ini.Save();
    }

    /// <summary>Changes only the multiplier keys, leaving every other addon setting as the user left it.</summary>
    public static void ApplyMultiplier(string exeDir, int multiplier)
    {
        var ini = IniFile.Open(IniPath(exeDir));
        SetMultiplier(ini, multiplier);
        ini.Save();
    }

    /// <summary>Writes one pacing preset's keys; everything else in the section is left alone.</summary>
    public static void ApplyPacing(string exeDir, PacingPreset preset)
    {
        var ini = IniFile.Open(IniPath(exeDir));
        foreach (var (key, value) in Pacing.KeysFor(preset)) ini.Set(Section, key, value);
        ini.Save();
    }

    private static void SetMultiplier(IniFile ini, int multiplier)
    {
        ini.Set(Section, "Enabled", "1");
        ini.Set(Section, "ForceMultiplier", multiplier == Dynamic ? "0" : multiplier.ToString());
        ini.Set(Section, "DynamicMFG", multiplier == Dynamic ? "1" : "0");
        if (multiplier == Dynamic && ini.Get(Section, "DynamicTargetFPS") is null)
            ini.Set(Section, "DynamicTargetFPS", "0");   // 0 = follow display refresh
    }

    public static void Remove(string exeDir)
    {
        var path = IniPath(exeDir);
        if (!File.Exists(path)) return;
        var ini = IniFile.Open(path);
        ini.RemoveSection(Section);
        ini.Save();
    }
}
