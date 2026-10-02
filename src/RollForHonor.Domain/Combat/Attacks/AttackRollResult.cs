using RollForHonor.Domain.Dice;

namespace RollForHonor.Domain.Combat.Attacks;

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
}
