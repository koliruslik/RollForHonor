using RollForHonor.Domain.Combat.Damage.Models;
using RollForHonor.Domain.Dice.Services;

namespace RollForHonor.Domain.Combat.Damage.Services;

/// <summary>Rolls damage components without requiring or applying an attack outcome.</summary>
public sealed class RawDamageResolver : IRawDamageResolver
{
    /// <inheritdoc />
    public DamageCalculationResult Resolve(IReadOnlyList<DamageComponent> components, IDiceRoller dice)
    {
        ArgumentNullException.ThrowIfNull(components);
        ArgumentNullException.ThrowIfNull(dice);
        if (components.Any(component => component is null))
            throw new ArgumentException("Damage components cannot contain null.", nameof(components));

        var amounts = components.Select(component =>
        {
            var roll = dice.Roll(component.Dice);
            var amount = Math.Max(0, checked(roll.Total + component.FlatDamage));
            return new DamageAmount(component.Type, amount);
        }).ToArray();
        var damage = new DamagePacket(amounts);
        var breakdown = new DamageBreakdown([
            new DamageStep(DamageStepType.WeaponRoll, new DamagePacket([]), damage)
        ]);
        return new DamageCalculationResult(breakdown, damage);
    }
}
