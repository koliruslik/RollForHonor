using System.Globalization;
using RollForHonor.Domain.Dice.Models;

namespace RollForHonor.Domain.Combat.Attacks.Models;

/// <summary>
/// Contains an attack roll, its modifier, and the resulting total.
/// </summary>
public sealed record AttackRollResult
{
    /// <summary>Gets the underlying dice roll.</summary>
    public DiceRoll Roll { get; }

    /// <summary>Gets the modifier applied to the roll.</summary>
    public int Modifier { get; }

    /// <summary>Gets the final attack-roll total.</summary>
    public int Total { get; }

    /// <summary>
    /// Creates a validated attack-roll result.
    /// </summary>
    /// <param name="roll">The underlying dice roll.</param>
    /// <param name="modifier">The flat attack modifier.</param>
    /// <param name="total">The roll total plus the modifier.</param>
    /// <exception cref="ArgumentNullException"><paramref name="roll"/> is null.</exception>
    /// <exception cref="ArgumentException"><paramref name="total"/> does not match the roll and modifier.</exception>
    public AttackRollResult(
        DiceRoll roll,
        int modifier,
        int total)
    {
        ArgumentNullException.ThrowIfNull(roll);

        if (roll.Total + modifier != total)
        {
            throw new ArgumentException(
                "The total must equal the roll total plus the modifier.",
                nameof(total));
        }

        Roll = roll;
        Modifier = modifier;
        Total = total;
    }

    /// <inheritdoc />
    public override string ToString()
    {
        var results = string.Join(
            ", ",
            Roll.Results.Select(result => result.ToString(CultureInfo.InvariantCulture)));

        var modifier = Modifier switch
        {
            > 0 => $" + {Modifier.ToString(CultureInfo.InvariantCulture)}",
            < 0 => $" - {Math.Abs((long)Modifier).ToString(CultureInfo.InvariantCulture)}",
            _ => string.Empty
        };

        return $"{Roll.Formula} [{results}]{modifier} = {Total}";
    }
}
