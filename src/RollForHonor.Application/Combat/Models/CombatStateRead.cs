using RollForHonor.Domain.Combat.State.Models;

namespace RollForHonor.Application.Combat.Models;

/// <summary>A consistent, immutable read of current combatant data and its expected commit version.</summary>
/// <remarks>
/// Reuses Domain combatant views without specifying a mutable runtime entity or storage technology.
/// Adapters own extraction from live state. Snapshot builders assemble the per-impact state snapshot.
/// </remarks>
public sealed record CombatStateRead
{
    /// <summary>Gets the version that must still match when the result is committed.</summary>
    public long Version { get; }
    /// <summary>Gets current source data, including currently active effects.</summary>
    public CombatantSnapshot Source { get; }
    /// <summary>Gets current target data from the same read.</summary>
    public CombatantSnapshot Target { get; }

    /// <summary>Captures a consistent read supplied by an adapter.</summary>
    /// <param name="version">The non-negative version covering both participants.</param>
    /// <param name="source">Immutable current source data.</param>
    /// <param name="target">Immutable current target data; reuse source for a self-target read.</param>
    /// <exception cref="ArgumentNullException">A participant is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">The version is negative.</exception>
    public CombatStateRead(long version, CombatantSnapshot source, CombatantSnapshot target)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(version);
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(target);
        Version = version;
        Source = source;
        Target = target;
    }
}
