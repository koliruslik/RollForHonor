using RollForHonor.Domain.Combat.Attacks.Models;
using RollForHonor.Domain.Combat.Damage.Models;
using RollForHonor.Domain.Combat.Effects.Models;
using RollForHonor.Domain.Combat.State.Models;

namespace RollForHonor.Domain.Combat.Resolution.Models;

/// <summary>
/// Describes the calculated result of an attack against one target.
/// </summary>
public sealed record TargetCombatResult
{
    /// <summary>Gets the target combatant identifier.</summary>
    public CombatantId TargetId { get; }

    /// <summary>Gets the resolved attack roll.</summary>
    public AttackRollResult AttackRoll { get; }

    /// <summary>Gets the attack outcome category.</summary>
    public AttackOutcome Outcome { get; }

    /// <summary>Gets damage before and after mitigation.</summary>
    public DamageResolution DamageResolution { get; }

    /// <summary>Gets effects reported for the target.</summary>
    public IReadOnlyList<ResolvedCombatEffect> Effects { get; }

    /// <summary>Gets whether the target is defeated after the resolution.</summary>
    public bool IsDefeated { get; }

    /// <summary>
    /// Creates an immutable target combat result.
    /// </summary>
    public TargetCombatResult(
        CombatantId targetId,
        AttackRollResult attackRoll,
        AttackOutcome outcome,
        DamageResolution damageResolution,
        IReadOnlyList<ResolvedCombatEffect> effects,
        bool isDefeated)
    {
        ArgumentNullException.ThrowIfNull(targetId);
        ArgumentNullException.ThrowIfNull(attackRoll);
        ArgumentNullException.ThrowIfNull(damageResolution);
        ArgumentNullException.ThrowIfNull(effects);

        if (!Enum.IsDefined(outcome))
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
