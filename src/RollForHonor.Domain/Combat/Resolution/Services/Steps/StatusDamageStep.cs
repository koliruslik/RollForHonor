using RollForHonor.Domain.Combat.Damage.Models;
using RollForHonor.Domain.Combat.Effects.Models;
using RollForHonor.Domain.Combat.Resolution.Models;
using RollForHonor.Domain.Dice.Services;

namespace RollForHonor.Domain.Combat.Resolution.Services.Steps;

/// <summary>Applies a captured dedicated status-damage coefficient before optional source scaling and defenses.</summary>
public sealed class StatusDamageStep : ICombatStep
{
    /// <inheritdoc />
    /// <exception cref="InvalidOperationException">Raw periodic damage is absent or ordinary source scaling already ran.</exception>
    /// <exception cref="OverflowException">Scaled damage exceeds supported bounds.</exception>
    public CombatContext Execute(CombatContext context, IDiceRoller dice)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(dice);

        if (context.Request.TickEffect?.Definition is not PeriodicDamageEffectDefinition definition)
        {
            throw new InvalidOperationException("Dedicated status scaling requires a periodic effect.");
        }

        var raw = context.BeforeDefenses ?? throw new InvalidOperationException("Status scaling requires raw damage.");

        if (context.SourceDamageResolved || context.AfterDefenses is not null
            || raw.Breakdown.Steps.Any(step => step.Origin == "CapturedStatusDamage"))
        {
            throw new InvalidOperationException("Status scaling must precede source scaling and defenses.");
        }

        var packet = new DamagePacket(raw.Damage.Components
            .Select(component => new DamageAmount(component.Type,
                checked((int)decimal.Ceiling(component.Amount * definition.DamageMultiplier))))
            .ToArray());
        var steps = raw.Breakdown.Steps.Append(new DamageStep(DamageStepType.AttackerModifier,
            raw.Damage, packet, definition.DamageMultiplier, "CapturedStatusDamage"));
        return context with { BeforeDefenses = new(new DamageBreakdown(steps.ToArray()), packet) };
    }
}
