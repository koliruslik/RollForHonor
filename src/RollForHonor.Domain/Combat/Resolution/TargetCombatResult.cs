using RollForHonor.Domain.Combat.Attacks;
using RollForHonor.Domain.Combat.Damage;
using RollForHonor.Domain.Combat.Effects;
using RollForHonor.Domain.Combat.State;

namespace RollForHonor.Domain.Combat.Resolution;

public sealed record TargetCombatResult
{
    public CombatantId TargetId { get; }
    public AttackRollResult AttackRoll { get; }
    public AttackOutcome Outcome { get; }
    public DamageResolution DamageResolution { get; }
    public IReadOnlyList<ResolvedCombatEffect> Effects { get; }
    public bool IsDefeated { get; }

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
