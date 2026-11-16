using RollForHonor.Domain.Combat.Damage.Models;
using RollForHonor.Domain.Dice.Services;

namespace RollForHonor.Domain.Combat.Damage.Services;

/// <summary>Rolls damage components without requiring or applying an attack outcome.</summary>
/// <remarks>The WeaponRoll stage retains each actual component roll independently of optional diagnostic capture.</remarks>
public sealed class RawDamageResolver : IRawDamageResolver
{
    /// <inheritdoc />
    /// <exception cref="ArgumentException">A dice source returns a formula that differs from its requested component.</exception>
    public DamageCalculationResult Resolve(IReadOnlyList<DamageComponent> components, IDiceRoller dice)
    {
        ArgumentNullException.ThrowIfNull(components);
        ArgumentNullException.ThrowIfNull(dice);
        if (components.Any(component => component is null))
            throw new ArgumentException("Damage components cannot contain null.", nameof(components));

        var rolls = components.Select(component =>
            new DamageComponentRoll(component, dice.Roll(component.Dice))).ToArray();
        var damage = new DamagePacket(rolls.Select(result =>
            new DamageAmount(result.Component.Type, result.Amount)).ToArray());
        var breakdown = new DamageBreakdown([
            new DamageStep(DamageStepType.WeaponRoll, new DamagePacket([]), damage, componentRolls: rolls)
        ]);
        return new DamageCalculationResult(breakdown, damage);
    }
}
