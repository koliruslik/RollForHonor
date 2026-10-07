using RollForHonor.Domain.Combat.Resolution.Services;

namespace RollForHonor.Domain.Combat.Resolution.Models;

/// <summary>
/// Represents a successfully calculated combat resolution.
/// </summary>
public sealed record ResolvedCombat : ICombatResolutionOutcome
{
    /// <summary>Gets the successfully calculated resolution.</summary>
    public CombatResolution Resolution { get; }

    /// <summary>
    /// Creates a resolved combat outcome.
    /// </summary>
    public ResolvedCombat(CombatResolution resolution)
    {
        ArgumentNullException.ThrowIfNull(resolution);
        Resolution = resolution;
    }
}
