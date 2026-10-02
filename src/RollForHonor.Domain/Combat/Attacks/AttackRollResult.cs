using RollForHonor.Domain.Dice;

namespace RollForHonor.Domain.Combat.Attacks;

public sealed record AttackRollResult
{
    public DiceRoll Roll { get; }

    public int Modifier { get; }

    public int Total { get; }

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
