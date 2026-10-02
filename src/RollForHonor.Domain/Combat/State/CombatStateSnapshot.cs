namespace RollForHonor.Domain.Combat.State;

public sealed record CombatStateSnapshot
{
    public long Version { get; }

    public CombatantSnapshot Source { get; }

    public CombatantSnapshot Target { get; }

    public CombatStateSnapshot(
        long version,
        CombatantSnapshot source,
        CombatantSnapshot target)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(version);
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(target);

        Version = version;
        Source = source;
        Target = target;
    }
}
