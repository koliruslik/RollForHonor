using RollForHonor.Domain.Combat.Damage.Models;
using RollForHonor.Domain.Combat.Resolution.Models;
using RollForHonor.Domain.Dice.Services;

namespace RollForHonor.Domain.Combat.Resolution.Services.Steps;

/// <summary>Adds typed flat attack damage and applies resolved source coefficients before hit quality and defenses without extra dice.</summary>
public sealed class SourceDamageStep : ICombatStep
{
    /// <inheritdoc />
    /// <exception cref="InvalidOperationException">Raw damage is absent, source scaling already ran, or quality/defenses already ran.</exception>
    /// <exception cref="OverflowException">Scaled component damage exceeds Int32 range.</exception>
    public CombatContext Execute(CombatContext context, IDiceRoller dice)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(dice);
        var raw = context.BeforeDefenses ?? throw new InvalidOperationException("Source scaling requires raw damage.");

        if (context.SourceDamageResolved || context.AfterDefenses is not null || raw.Breakdown.Steps.Any(step =>
            step.Type == DamageStepType.AttackQuality || step.Origin?.StartsWith("CharacterStats:", StringComparison.Ordinal) == true))
        {
            throw new InvalidOperationException("Source scaling must run once before quality and defenses.");
        }

        var packet = raw.Damage;
        var steps = raw.Breakdown.Steps.ToList();

        var added = context.Request.StateSnapshot.Source.Offense.AddedDamage;

        if (context.Request.Attack is not null && added.Count > 0)
        {
            var next = new DamagePacket(packet.Components
                .Concat(added)
                .ToArray());
            steps.Add(new DamageStep(DamageStepType.AttackerModifier, packet, next, origin: "CharacterStats:AddedDamage", flatBonus: added.Sum(value => value.Amount)));
            packet = next;
        }

        var types = packet.Components
            .Select(component => component.Type)
            .Distinct();

        foreach (var type in types)
        {
            var multiplier = type == DamageType.Pure
                || context.Request.TickEffect?.Definition is RollForHonor.Domain.Combat.Effects.Models.PeriodicDamageEffectDefinition { ScaleWithSource: false } ? 1 : context.Request.StateSnapshot.Source.Offense.DamageMultiplier(type);

            if (multiplier == 1)
            {
                continue;
            }

            var next = new DamagePacket(packet.Components
                .Select(component => component.Type == type
                    ? new DamageAmount(type, checked((int)decimal.Ceiling(component.Amount * multiplier)))
                    : component)
                .ToArray());
            steps.Add(new DamageStep(DamageStepType.AttackerModifier, packet, next, multiplier, $"CharacterStats:{type}"));
            packet = next;
        }

        return context with
        {
            BeforeDefenses = new DamageCalculationResult(new DamageBreakdown(steps), packet),
            SourceDamageResolved = true
        };
    }
}
