using RollForHonor.Domain.Combat.State.Models;

namespace RollForHonor.Domain.Combat.Resolution.Models;

/// <summary>
/// Describes the source and target result of one combat impact.
/// </summary>
public sealed record CombatResult
{
    /// <summary>Gets the attacking combatant identifier.</summary>
    public CombatantId SourceId { get; }

    /// <summary>Gets the calculated target result.</summary>
    public TargetCombatResult Target { get; }

    /// <summary>
    /// Creates a combat result for one source and target.
    /// </summary>
    /// <param name="sourceId">The attacking combatant identifier.</param>
    /// <param name="target">The complete result calculated for the target.</param>
    /// <exception cref="ArgumentNullException">Either argument is null.</exception>
    public CombatResult(
        CombatantId sourceId,
        TargetCombatResult target)
    {
        ArgumentNullException.ThrowIfNull(sourceId);
        ArgumentNullException.ThrowIfNull(target);

        SourceId = sourceId;
        Target = target;
    }
}
