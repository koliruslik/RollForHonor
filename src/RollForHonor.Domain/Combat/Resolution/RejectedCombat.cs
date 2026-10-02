namespace RollForHonor.Domain.Combat.Resolution;

public sealed record RejectedCombat : ICombatResolutionOutcome
{
    public CombatRejection Rejection { get; }

    public RejectedCombat(CombatRejection rejection)
    {
        ArgumentNullException.ThrowIfNull(rejection);
        Rejection = rejection;
    }
}
