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

    /// <summary>Gets the multiplier applied after this component's dice and flat base, before adding separate affix components.</summary>
    public decimal Multiplier { get; }

    /// <summary>
    /// Creates a validated attack damage component.
    /// </summary>
    /// <param name="dice">The dice rolled to produce this component.</param>
    /// <param name="flatDamage">The signed flat value added after the roll.</param>
    /// <param name="type">A defined damage type.</param>
    /// <param name="multiplier">A positive captured base-roll multiplier; defaults to one.</param>
    /// <exception cref="ArgumentNullException"><paramref name="dice"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">The damage type is undefined or the multiplier is not positive.</exception>
    public DamageComponent(
        DiceFormula dice,
        int flatDamage,
        DamageType type,
        decimal multiplier = 1)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(multiplier);
        Multiplier = multiplier;
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

        var formula = $"{Dice}{flatDamage}";
        return Multiplier == 1 ? $"{formula} [{Type}]"
            : $"({formula}) ×{Multiplier.ToString("0.####", CultureInfo.InvariantCulture)} [{Type}]";
    }
}
