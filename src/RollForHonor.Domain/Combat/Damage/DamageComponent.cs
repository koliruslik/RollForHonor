using System.Globalization;
using RollForHonor.Domain.Dice;

namespace RollForHonor.Domain.Combat.Damage;

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
