namespace Nakawari.Models;

public enum HealthState
{
    /// <summary>No evidence yet: the game hasn't run since Nakawari set it up.</summary>
    NotRunYet,
    /// <summary>The addon loaded and unlocked frame generation in the game's own DLSS-G.</summary>
    Working,
    /// <summary>ReShade never started with the game (wrong folder, blocked proxy).</summary>
    ReShadeNotLoading,
    /// <summary>The addon loaded but could not unlock the game's DLSS-G.</summary>
    Problem,
    /// <summary>Log wording this version of Nakawari doesn't recognise. Never shown as a problem.</summary>
    Unknown,
}

/// <summary>What the last play session's ReShade.log says about one game.</summary>
public sealed record GameHealth(HealthState State, int? MaxMultiplier, string Detail, DateTime? SeenAt)
{
    public static GameHealth NotRunYet { get; } = new(HealthState.NotRunYet, null, "Play the game once and Nakawari will check it worked.", null);
}
