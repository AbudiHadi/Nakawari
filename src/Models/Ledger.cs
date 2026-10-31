namespace Nakawari.Models;

/// <summary>
/// Exactly what Nakawari put into one game folder, so "Disable" can undo it precisely
/// and "Update all" can run from ledgers alone without rescanning every library.
/// </summary>
public sealed class Ledger
{
    public string GameId { get; set; } = "";
    public string GameName { get; set; } = "";
    public string ExePath { get; set; } = "";

    /// <summary>The game's install folder; lets Nakawari follow a game that was moved to another drive.</summary>
    public string? InstallDir { get; set; }
    public string? AddonTag { get; set; }
    public bool CreatedIni { get; set; }
    public List<LedgerFile> Files { get; set; } = [];

    /// <summary>The user's multiplier choice (0 = game menu). Source of truth; ReShade.ini is rewritten from it.</summary>
    public int Multiplier { get; set; }

    /// <summary>When Nakawari last changed this folder. Crashes before this moment are not ours.</summary>
    public DateTime LastChangedAt { get; set; } = DateTime.Now;

    /// <summary>Addon build that crashed this game; it is skipped until a newer build ships.</summary>
    public string? BadTag { get; set; }

    /// <summary>Frame-pacing preset written into the addon settings.</summary>
    public PacingPreset Pacing { get; set; }

    /// <summary>Streamline version of the 50-series kit installed in this game, or null.</summary>
    public string? KitVersion { get; set; }

    /// <summary>Addon build held in place after a rollback.</summary>
    public string? PinnedTag { get; set; }

    [System.Text.Json.Serialization.JsonIgnore]
    public string ExeDir => Path.GetDirectoryName(ExePath)!;

    public LedgerFile? Find(string role) => Files.FirstOrDefault(f => f.Role == role);

    public void Track(string path, string role, string? backupPath = null)
    {
        if (Files.Any(f => f.Path.Equals(path, StringComparison.OrdinalIgnoreCase))) return;
        Files.Add(new LedgerFile { Path = path, Role = role, BackupPath = backupPath });
    }

    public void Touch() => LastChangedAt = DateTime.Now;

    /// <summary>Re-points every tracked path after the game was moved (e.g. Steam "Move install folder").</summary>
    public void MoveTo(string newInstallDir, string newExePath)
    {
        var (oldRoot, newRoot) = InstallDir is null
            ? (ExeDir, Path.GetDirectoryName(newExePath)!)
            : (InstallDir, newInstallDir);
        foreach (var file in Files.Where(f => f.Path.StartsWith(oldRoot, StringComparison.OrdinalIgnoreCase)))
            file.Path = newRoot + file.Path[oldRoot.Length..];
        ExePath = newExePath;
        InstallDir = newInstallDir;
    }
}

public sealed class LedgerFile
{
    public const string Addon = "addon";
    public const string ReShade = "reshade";
    public const string Dlssg = "dlssg";
    public const string Legacy = "legacy";
    public const string Streamline = "streamline";

    public string Path { get; set; } = "";
    public string Role { get; set; } = "";
    public string? BackupPath { get; set; }
}
