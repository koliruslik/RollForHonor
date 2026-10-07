using RollForHonor.Domain.Combat.Attacks;
using RollForHonor.Domain.Combat.Damage;
using RollForHonor.Domain.Combat.State;
using RollForHonor.Domain.Dice;

namespace RollForHonor.Domain.Combat.Services;

public class AttackDamageResolver : IAttackDamageResolver
{
    private readonly DamageMultipliers _multipliers;

    public AttackDamageResolver(DamageMultipliers multipliers)
    {
        ArgumentNullException.ThrowIfNull(multipliers);

        _multipliers = multipliers;
    }

    public UnmitigatedDamage Resolve(
        AttackPayload attack,
        CombatantSnapshot source,
        AttackRollResult attackRoll,
        AttackOutcome outcome,
        IDiceRoller dice)
    {
        ArgumentNullException.ThrowIfNull(attack);
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(attackRoll);
        ArgumentNullException.ThrowIfNull(dice);
        List<DamageStep> steps = [];

        var multiplier = GetDamageMultiplier(outcome);

        var emptyDamage = new DamagePacket([]);

        var baseDamage = ResolveComponents(attack.Damage, dice);
        steps.Add(new DamageStep(DamageStepType.WeaponRoll, emptyDamage, baseDamage));

        var damageWithModifiers = baseDamage;
        //var damageWithModifiers = ApplyAttackerModifiers(source, baseDamage);
        //steps.Add(new DamageStep(DamageStepType.AttackerModifier, baseDamage, damageWithModifiers));

        var multipliedDamage = MultiplyDamage(multiplier, damageWithModifiers);
        steps.Add(new DamageStep(DamageStepType.AttackQuality, damageWithModifiers, multipliedDamage));

        return new UnmitigatedDamage(new DamageBreakdown(steps), multipliedDamage);
    }

    private static DamagePacket ResolveComponents(IReadOnlyList<DamageComponent> components, IDiceRoller dice)
    {
        var amounts = components.Select(component =>
        {
            var roll = dice.Roll(component.Dice);
            var rawAmount = checked(roll.Total + component.FlatDamage);
            var amount = Math.Max(0, rawAmount);

            return new DamageAmount(component.Type, amount);
        }).ToArray();

        return new DamagePacket(amounts);
    }

    private static DamagePacket ApplyAttackerModifiers(CombatantSnapshot source, DamagePacket damage)
    {
        // TODO
        return damage;
    }

    private static DamagePacket MultiplyDamage(decimal multiplier, DamagePacket damagePacket)
    {
        var multipliedDamage = damagePacket.Components
            .Select(damage =>
            {
                var scaledAmount = Math.Ceiling(damage.Amount * multiplier);
                var amount = checked((int)scaledAmount);
                return new DamageAmount(damage.Type, amount);
            }).ToList();
        return new DamagePacket(multipliedDamage);
    }

    private decimal GetDamageMultiplier(AttackOutcome outcome)
    {
        return outcome switch
        {
            AttackOutcome.Evaded => _multipliers.Evaded,
            AttackOutcome.GlancingHit => _multipliers.GlancingHit,
            AttackOutcome.Hit => _multipliers.Hit,
            AttackOutcome.CriticalHit => _multipliers.CriticalHit,
            _ => throw new InvalidOperationException($"Unknown attack outcome {outcome}"),
        };
    }
}
