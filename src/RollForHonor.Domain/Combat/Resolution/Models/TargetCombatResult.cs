using RollForHonor.Domain.Combat.Attacks.Models;
using RollForHonor.Domain.Combat.Damage.Models;
using RollForHonor.Domain.Combat.Effects.Models;
using RollForHonor.Domain.Combat.State.Models;

namespace RollForHonor.Domain.Combat.Resolution.Models;

/// <summary>
/// Describes an action against one target, including actions without attack or damage stages.
/// </summary>
public sealed record TargetCombatResult
{
    /// <summary>Gets the target combatant identifier.</summary>
    public CombatantId TargetId { get; }

    /// <summary>Gets the resolved roll, or null if the pipeline contains no roll step.</summary>
    public AttackRollResult? AttackRoll { get; }

    /// <summary>Gets the attack outcome category, or null when no outcome step ran.</summary>
    public AttackOutcome? Outcome { get; }

    /// <summary>Gets damage before and after mitigation, or null for non-damaging actions.</summary>
    public DamageResolution? DamageResolution { get; }

    /// <summary>Gets effects reported by the action, each with its actual recipient ID.</summary>
    public IReadOnlyList<ResolvedCombatEffect> Effects { get; }

    /// <summary>Gets whether the target is defeated after the resolution.</summary>
    public bool IsDefeated { get; }

    /// <summary>
    /// Creates an immutable target combat result.
    /// </summary>
    /// <param name="targetId">The affected combatant identifier.</param>
    /// <param name="attackRoll">The resolved attack roll, or null when omitted.</param>
    /// <param name="outcome">A defined attack outcome, or null when omitted.</param>
    /// <param name="damageResolution">Damage stages, or null for non-damaging actions.</param>
    /// <param name="effects">Effects reported by the action, with explicit recipient identities.</param>
    /// <param name="isDefeated">Whether projected final health is zero.</param>
    /// <exception cref="ArgumentNullException">The target ID or effect collection is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="outcome"/> is undefined.</exception>
    /// <exception cref="ArgumentException"><paramref name="effects"/> contains a null item.</exception>
    public TargetCombatResult(
        CombatantId targetId,
        AttackRollResult? attackRoll,
        AttackOutcome? outcome,
        DamageResolution? damageResolution,
        IReadOnlyList<ResolvedCombatEffect> effects,
        bool isDefeated)
    {
        ArgumentNullException.ThrowIfNull(targetId);
        ArgumentNullException.ThrowIfNull(effects);

        if (outcome is not null && !Enum.IsDefined(outcome.Value))
        {
            throw new ArgumentOutOfRangeException(nameof(outcome));
        }

        if (effects.Any(effect => effect is null))
        {
            throw new ArgumentException(
                "A target result cannot contain null effects.",
                nameof(effects));
        }

        TargetId = targetId;
        AttackRoll = attackRoll;
        Outcome = outcome;
        DamageResolution = damageResolution;
        Effects = effects.ToArray();
        IsDefeated = isDefeated;
    }
}
