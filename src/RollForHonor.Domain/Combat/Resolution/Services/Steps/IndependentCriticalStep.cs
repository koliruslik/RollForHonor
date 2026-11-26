using RollForHonor.Domain.Combat.Attacks.Models;
using RollForHonor.Domain.Combat.Damage.Models;
using RollForHonor.Domain.Combat.Resolution.Models;
using RollForHonor.Domain.Dice.Services;

namespace RollForHonor.Domain.Combat.Resolution.Services.Steps;

/// <summary>Rolls a separate critical d20 after quality, preserving glancing quality and actual provenance.</summary>
public sealed class IndependentCriticalStep : ICombatStep
{
    /// <inheritdoc />
    /// <exception cref="InvalidOperationException">Quality is unresolved, defenses ran or this check already ran.</exception>
    public CombatContext Execute(CombatContext context, IDiceRoller dice)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(dice);
        var raw = context.BeforeDefenses ?? throw new InvalidOperationException("Critical checks require resolved damage.");

        if (context.AttackOutcome is null || context.AfterDefenses is not null
            || !raw.Breakdown.Steps.Any(step => step.Type == DamageStepType.AttackQuality)
            || raw.Breakdown.Steps.Any(step => step.Type == DamageStepType.CriticalCheck))
        {
            throw new InvalidOperationException("Critical checks run once after attack quality and before defense.");
        }

        if (context.AttackOutcome == AttackOutcome.Evaded)
        {
            return context;
        }

        var roll = dice.Roll(new(1, 20));
        var offense = context.Request.StateSnapshot.Source.Offense;
        var multiplier = (long)roll.Total + offense.CriticalRollBonus >= 20 ? offense.CriticalMultiplier : 1;
        var output = new DamagePacket(raw.Damage.Components
            .Select(component => new DamageAmount(component.Type, checked((int)decimal.Ceiling(component.Amount * multiplier))))
            .ToArray());
        var steps = raw.Breakdown.Steps.Append(new DamageStep(DamageStepType.CriticalCheck, raw.Damage, output,
            multiplier, "Independent d20 critical", offense.CriticalRollBonus, decisionRoll: roll)).ToArray();

        return context with { BeforeDefenses = new(new DamageBreakdown(steps), output) };
    }
}
