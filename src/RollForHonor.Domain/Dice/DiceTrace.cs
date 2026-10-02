namespace RollForHonor.Domain.Dice;

public sealed record DiceTrace
{
    public IReadOnlyList<DiceRoll> Rolls { get; }

    public DiceTrace(IReadOnlyList<DiceRoll> rolls)
    {
        ArgumentNullException.ThrowIfNull(rolls);

        if (rolls.Any(roll => roll is null))
        {
            throw new ArgumentException(
                "The trace cannot contain null rolls.",
                nameof(rolls));
        }

        Rolls = rolls.ToArray();
    }
}
