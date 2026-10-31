namespace Nakawari.Models;

/// <summary>A game found in a store library (or added by hand).</summary>
public sealed record GameEntry(string Id, string Name, string Store, string InstallDir, string? ExeHint = null);
