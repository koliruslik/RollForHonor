using RollForHonor.Domain.Combat.Resolution.Services;

namespace RollForHonor.Domain.Combat.Resolution.Models;

/// <summary>
/// Represents a combat request rejected before state changes were produced.
/// </summary>
public sealed record RejectedCombat : ICombatResolutionOutcome
{
    /// <summary>Gets the reason the request was rejected.</summary>
    public CombatRejection Rejection { get; }

    /// <summary>
    /// Creates a rejected combat outcome.
    /// </summary>
    /// <param name="rejection">The validated reason resolution did not begin.</param>
    /// <exception cref="ArgumentNullException"><paramref name="rejection"/> is null.</exception>
    public RejectedCombat(CombatRejection rejection)
    {
        ArgumentNullException.ThrowIfNull(rejection);
        Rejection = rejection;
    }
}
