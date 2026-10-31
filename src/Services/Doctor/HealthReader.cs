using System.Text.Json;
using Nakawari.Models;

namespace Nakawari.Services.Doctor;

/// <summary>
/// One entry point for "did frame generation work last session?". Prefers the addon's own
/// mfgunlock-status.json (exact facts, written by addon builds that support it) and falls back to
/// reading ReShade.log for older addon builds.
/// </summary>
public static class HealthReader
{
    private const string StatusFile = "mfgunlock-status.json";

    public static GameHealth Read(string exeDir, DateTime changedAt) =>
        FromStatusFile(exeDir, changedAt) ?? ReShadeLogReader.Read(exeDir, changedAt);

    private static GameHealth? FromStatusFile(string exeDir, DateTime changedAt)
    {
        var path = Path.Combine(exeDir, StatusFile);
        if (!File.Exists(path)) return null;
        var seenAt = File.GetLastWriteTime(path);
        if (seenAt < changedAt) return GameHealth.NotRunYet;

        try
        {
            using var doc = JsonDocument.Parse(File.ReadAllText(path));
            var root = doc.RootElement;
            if (root.GetProperty("schema").GetInt32() != 1) return null;   // unknown schema: let the log reader decide

            var max = root.GetProperty("max_multiplier").GetInt32() is > 0 and var m ? m : (int?)null;
            var unlocked = root.GetProperty("unlocked").GetBoolean();
            var runtimeOk = root.GetProperty("runtime_status").GetInt64() == 0;
            var refusedGameCopy = root.GetProperty("refused_providers").EnumerateArray()
                .Any(p => p.GetString() is { } s && !s.Contains(@"\DriverStore\", StringComparison.OrdinalIgnoreCase));

            if (unlocked || runtimeOk)
                return new GameHealth(HealthState.Working, max, "Frame generation unlocked in the last session (addon status file).", seenAt);
            if (refusedGameCopy)
                return new GameHealth(HealthState.Problem, max, "The addon doesn't recognise this game's DLSS-G version. Try Upgrade old DLSS-G.", seenAt);
            return new GameHealth(HealthState.NotRunYet, max, "The addon started but frame generation wasn't used yet. Turn it on in the game.", seenAt);
        }
        catch (Exception ex) when (ex is JsonException or IOException or KeyNotFoundException or InvalidOperationException)
        {
            return null;
        }
    }
}
