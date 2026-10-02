namespace RollForHonor.Domain.Combat.Resolution;

public sealed record ResolvedCombat : ICombatResolutionOutcome
{
    public CombatResolution Resolution { get; }

    public ResolvedCombat(CombatResolution resolution)
    {
        ArgumentNullException.ThrowIfNull(resolution);
        Resolution = resolution;
    }
}
