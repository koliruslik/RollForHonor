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
    /// <param name="targetId">The affected combatant identifier.</param>
    /// <param name="attackRoll">The resolved attack roll.</param>
    /// <param name="outcome">A defined attack outcome.</param>
    /// <param name="damageResolution">Damage before and after defenses.</param>
    /// <param name="effects">Effects reported for this target.</param>
    /// <param name="isDefeated">Whether projected final health is zero.</param>
    /// <exception cref="ArgumentNullException">A reference argument is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="outcome"/> is undefined.</exception>
    /// <exception cref="ArgumentException"><paramref name="effects"/> contains a null item.</exception>
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
