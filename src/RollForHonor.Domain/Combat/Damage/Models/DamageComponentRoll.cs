using RollForHonor.Domain.Dice.Models;

namespace RollForHonor.Domain.Combat.Damage.Models;

/// <summary>Retains one component's actual dice, flat base and captured post-roll base multiplier.</summary>
public sealed record DamageComponentRoll
{
    /// <summary>Gets the original typed formula and flat damage, without inferred modifier origins.</summary>
    public DamageComponent Component { get; }
    /// <summary>Gets the actual roll returned by the shared dice source, including every die value.</summary>
    public DiceRoll Roll { get; }
    /// <summary>Gets zero-clamped roll plus flat damage times the base multiplier, rounded up before quality or defenses.</summary>
    public int Amount { get; }

    /// <summary>Calculates the component amount once from its actual roll and supplied flat damage.</summary>
    /// <param name="component">The immutable typed damage formula and flat value.</param>
    /// <param name="roll">The actual dice result matching the component formula.</param>
    /// <exception cref="ArgumentNullException">Component or roll is null.</exception>
    /// <exception cref="ArgumentException">The roll formula differs from the component formula.</exception>
    /// <exception cref="OverflowException">Base addition or the scaled integer amount exceeds its numeric range.</exception>
    public DamageComponentRoll(DamageComponent component, DiceRoll roll)
    {
        ArgumentNullException.ThrowIfNull(component);
        ArgumentNullException.ThrowIfNull(roll);
        if (component.Dice != roll.Formula)
        {
            throw new ArgumentException("The roll must match the component dice formula.", nameof(roll));
        }

        Component = component;
        Roll = roll;
        Amount = checked((int)decimal.Ceiling(Math.Max(0, checked(roll.Total + component.FlatDamage)) * component.Multiplier));
    }
}
