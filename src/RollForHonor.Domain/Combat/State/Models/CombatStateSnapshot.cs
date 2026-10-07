namespace RollForHonor.Domain.Combat.State.Models;

/// <summary>
/// Captures the versioned source and target state used by one resolution.
/// </summary>
public sealed record CombatStateSnapshot
{
    /// <summary>Gets the state version used for optimistic commit validation.</summary>
    public long Version { get; }

    /// <summary>Gets the attacking combatant snapshot.</summary>
    public CombatantSnapshot Source { get; }

    /// <summary>Gets the target combatant snapshot.</summary>
    public CombatantSnapshot Target { get; }

    /// <summary>
    /// Creates a versioned combat snapshot.
    /// </summary>
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
