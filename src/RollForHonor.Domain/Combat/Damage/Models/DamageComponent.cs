using System.Globalization;
using RollForHonor.Domain.Dice.Models;

namespace RollForHonor.Domain.Combat.Damage.Models;

/// <summary>
/// Defines one dice-based and flat component of attack damage.
/// </summary>
public sealed record DamageComponent
{
    /// <summary>Gets the dice formula of the component.</summary>
    public DiceFormula Dice { get; }

    /// <summary>Gets the flat damage added to the dice result.</summary>
    public int FlatDamage { get; }

    /// <summary>Gets the component's damage type.</summary>
    public DamageType Type { get; }

    /// <summary>
    /// Creates a validated attack damage component.
    /// </summary>
    /// <param name="dice">The dice rolled to produce this component.</param>
    /// <param name="flatDamage">The signed flat value added after the roll.</param>
    /// <param name="type">A defined damage type.</param>
    /// <exception cref="ArgumentNullException"><paramref name="dice"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="type"/> is undefined.</exception>
    public DamageComponent(
        DiceFormula dice,
        int flatDamage,
        DamageType type)
    {
        ArgumentNullException.ThrowIfNull(dice);

        if (!Enum.IsDefined(type))
        {
            throw new ArgumentOutOfRangeException(nameof(type));
        }

        Dice = dice;
        FlatDamage = flatDamage;
        Type = type;
    }

    /// <inheritdoc />
    public override string ToString()
    {
        var flatDamage = FlatDamage switch
        {
            > 0 => $" + {FlatDamage.ToString(CultureInfo.InvariantCulture)}",
            < 0 => $" - {Math.Abs((long)FlatDamage).ToString(CultureInfo.InvariantCulture)}",
            _ => string.Empty
        };

        return $"{Dice}{flatDamage} [{Type}]";
    }
}
