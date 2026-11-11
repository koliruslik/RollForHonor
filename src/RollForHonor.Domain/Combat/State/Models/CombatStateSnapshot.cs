namespace RollForHonor.Domain.Combat.State.Models;

/// <summary>
/// Captures the versioned calculation inputs used by one resolution.
/// For ticks, the source is historical and only the target represents current mutable state.
/// </summary>
public sealed record CombatStateSnapshot
{
    /// <summary>Gets the state version used for optimistic commit validation.</summary>
    public long Version { get; }

    /// <summary>Gets the current attack source or the source captured when an effect was applied.</summary>
    public CombatantSnapshot Source { get; }

    /// <summary>Gets the target combatant snapshot.</summary>
    public CombatantSnapshot Target { get; }

    /// <summary>
    /// Creates a versioned combat snapshot.
    /// </summary>
    /// <param name="version">The non-negative state version used for optimistic commit.</param>
    /// <param name="source">The current attack source or an effect's historical source snapshot.</param>
    /// <param name="target">The target combatant snapshot.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="version"/> is negative.</exception>
    /// <exception cref="ArgumentNullException">The source or target is null.</exception>
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
