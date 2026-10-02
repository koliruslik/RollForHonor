namespace RollForHonor.Domain.Combat.Resolution;

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
    public RejectedCombat(CombatRejection rejection)
    {
        ArgumentNullException.ThrowIfNull(rejection);
        Rejection = rejection;
    }
}
