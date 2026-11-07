using RollForHonor.Domain.Combat.Damage.Models;
using RollForHonor.Domain.Dice.Services;

namespace RollForHonor.Domain.Combat.Damage.Services;

/// <summary>Calculates typed damage independently of attack rolls and outcomes.</summary>
public interface IRawDamageResolver
{
    /// <summary>Rolls each component once, adds its flat bonus with checked arithmetic, and clamps it at zero.</summary>
    /// <param name="components">Ordered damage components; may be empty, but cannot contain null.</param>
    /// <param name="dice">The shared dice source recording the damage rolls.</param>
    /// <returns>Unscaled damage and its component-roll breakdown, without target defenses.</returns>
    /// <exception cref="ArgumentNullException">An argument is null.</exception>
    /// <exception cref="ArgumentException">A component is null.</exception>
    /// <exception cref="OverflowException">A roll plus its flat bonus exceeds the integer range.</exception>
    DamageCalculationResult Resolve(IReadOnlyList<DamageComponent> components, IDiceRoller dice);
}
