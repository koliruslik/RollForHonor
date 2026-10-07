using RollForHonor.Domain.Combat.Attacks.Models;
using RollForHonor.Domain.Combat.State.Models;
using RollForHonor.Domain.Dice.Models;

namespace RollForHonor.Domain.Combat.Attacks.Services;

/// <summary>
/// Classifies a d20 attack by its margin against target evasion and natural-roll shifts.
/// </summary>
public sealed class D20AttackOutcomePolicy : IAttackOutcomePolicy
{
    /// <inheritdoc />
    public AttackOutcome Determine(
        AttackPayload attack,
        CombatantSnapshot source,
        CombatantSnapshot target,
        AttackRollResult attackRoll)
    {
        ArgumentNullException.ThrowIfNull(attack);
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(attackRoll);

        if (!IsD20(attack.AttackRoll))
        {
            throw new ArgumentException(
                "Attack outcome rules require a 1d20 attack roll.",
                nameof(attack));
        }

        if (attackRoll.Roll.Formula != attack.AttackRoll)
        {
            throw new ArgumentException(
                "The attack roll formula must match the attack payload.",
                nameof(attackRoll));
        }

        var margin = (long)attackRoll.Total - target.Evasion;

        var outcome = margin switch
        {
            <= -10 => AttackOutcome.Evaded,
            < 0 => AttackOutcome.GlancingHit,
            < 10 => AttackOutcome.Hit,
            _ => AttackOutcome.CriticalHit
        };

        var naturalRoll = attackRoll.Roll.Results[0];

        if (naturalRoll <= attack.OutcomeShiftRules.DowngradeMaximum)
        {
            return Downgrade(outcome);
        }

        if (naturalRoll >= attack.OutcomeShiftRules.UpgradeMinimum)
        {
            return Upgrade(outcome);
        }

        return outcome;
    }

    private static bool IsD20(DiceFormula formula)
    {
        return formula.Count == 1 && formula.Sides == 20;
    }

    private static AttackOutcome Upgrade(AttackOutcome outcome)
    {
        return outcome switch
        {
            AttackOutcome.Evaded => AttackOutcome.GlancingHit,
            AttackOutcome.GlancingHit => AttackOutcome.Hit,
            AttackOutcome.Hit => AttackOutcome.CriticalHit,
            AttackOutcome.CriticalHit => AttackOutcome.CriticalHit,
            _ => throw new ArgumentOutOfRangeException(
                nameof(outcome),
                outcome,
                "Unknown attack outcome.")
        };
    }

    private static AttackOutcome Downgrade(AttackOutcome outcome)
    {
        return outcome switch
        {
            AttackOutcome.Evaded => AttackOutcome.Evaded,
            AttackOutcome.GlancingHit => AttackOutcome.Evaded,
            AttackOutcome.Hit => AttackOutcome.GlancingHit,
            AttackOutcome.CriticalHit => AttackOutcome.Hit,
            _ => throw new ArgumentOutOfRangeException(
                nameof(outcome),
                outcome,
                "Unknown attack outcome.")
        };
    }
}
