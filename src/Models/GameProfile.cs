namespace Nakawari.Models;

/// <summary>
/// Per-game fixes. Matched by a case-insensitive part of the game's name.
/// Built-in entries come from the MFG Unlock README; more arrive through profiles.json.
/// </summary>
public sealed record GameProfile(string Match, int? HdrMode = null, string? Note = null, int? Recommended = null)
{
    public static readonly GameProfile Default = new("");
}
