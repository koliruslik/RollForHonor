using RollForHonor.Domain.Combat.State.Models;
using RollForHonor.Domain.Resources.Models;

namespace RollForHonor.Application.Combat.Models;

/// <summary>A consistent adapter read of the source, resource balances, cooldown and request receipt.</summary>
public sealed record AttackInitiationState
{
    /// <summary>Gets the version covering the complete initiation read.</summary>
    public long Version { get; }
    /// <summary>Gets current source data and effects.</summary>
    public CombatantSnapshot Source { get; }
    /// <summary>Gets immutable resource balances.</summary>
    public IReadOnlyList<ResourceAmount> Resources { get; }
    /// <summary>Gets when this attack is next available on the monotonic game timeline.</summary>
    public TimeSpan ReadyAt { get; }
    /// <summary>Gets the original attack identity if this request was already accepted.</summary>
    public Guid? StartedAttackId { get; }
    /// <summary>Captures a consistent initiation read.</summary>
    /// <param name="version">The nonnegative expected commit version.</param>
    /// <param name="source">The current source.</param>
    /// <param name="resources">Unique non-null resource balances.</param>
    /// <param name="readyAt">The cooldown deadline on the game timeline.</param>
    /// <param name="startedAttackId">An optional receipt for this request.</param>
    public AttackInitiationState(long version, CombatantSnapshot source, IReadOnlyList<ResourceAmount> resources,
        TimeSpan readyAt, Guid? startedAttackId = null)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(version);
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(resources);
        var balances = resources.ToArray();
        if (balances.Any(value => value is null) || balances.Select(value => value.Resource).Distinct().Count() != balances.Length)
            throw new ArgumentException("Resource balances must be non-null and unique.", nameof(resources));
        if (startedAttackId == Guid.Empty) throw new ArgumentException("A receipt cannot have an empty attack identity.", nameof(startedAttackId));
        Version = version;
        Source = source;
        Resources = Array.AsReadOnly(balances);
        ReadyAt = readyAt;
        StartedAttackId = startedAttackId;
    }
}
