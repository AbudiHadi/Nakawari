using System.Text.Json;

namespace Nakawari.Services;

public enum ScsState { NotUsed, Recorder, Armed }

/// <summary>
/// Read-only view of SCSKiller (https://github.com/BlueHeisenberg/SCSKiller).
/// Two uses: borrow the game EXE SCSKiller already resolved, and show its recorder state per game.
/// This app never writes SCSKiller's files (see SafeFs) and never writes while a game runs,
/// because SCSKiller's install watcher disarms the recorder when a non-SCSKiller file changes.
/// </summary>
public sealed class ScsKillerBridge
{
    private static readonly string LocalAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
    private static readonly string GamesDir = Path.Combine(LocalAppData, "SCSKiller", "games");

    private readonly Lazy<Dictionary<string, string>> _exeByInstallDir = new(LoadExeHints);

    public bool IsInstalled => Directory.Exists(Path.Combine(LocalAppData, "SCSKiller.App")) || Directory.Exists(GamesDir);

    public string? ExeHint(string installDir) =>
        _exeByInstallDir.Value.TryGetValue(installDir.TrimEnd('\\'), out var exe) && File.Exists(exe) ? exe : null;

    public static ScsState StateOf(string? exeDir)
    {
        if (exeDir is null) return ScsState.NotUsed;
        if (File.Exists(Path.Combine(exeDir, "scskiller.armed"))) return ScsState.Armed;
        return File.Exists(Path.Combine(exeDir, "scskiller.ini")) ? ScsState.Recorder : ScsState.NotUsed;
    }

    private static Dictionary<string, string> LoadExeHints()
    {
        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (!Directory.Exists(GamesDir)) return map;

        foreach (var state in Directory.EnumerateFiles(GamesDir, "state.json", SearchOption.AllDirectories))
        {
            try
            {
                using var doc = JsonDocument.Parse(File.ReadAllText(state));
                var root = doc.RootElement;
                if (root.TryGetProperty("RecorderInstallDir", out var dir) && dir.ValueKind == JsonValueKind.String &&
                    root.TryGetProperty("RecorderExe", out var exe) && exe.ValueKind == JsonValueKind.String)
                {
                    map[dir.GetString()!.TrimEnd('\\')] = exe.GetString()!;
                }
            }
            catch (Exception ex) when (ex is JsonException or IOException) { /* a broken state file only costs a hint */ }
        }
        return map;
    }
}
