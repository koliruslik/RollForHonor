namespace RollForHonor.Domain.Dice;

public sealed record DiceFormula
{
    public int Count { get; }

    public int Sides { get; }

    public DiceFormula(int count, int sides)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(count, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(sides, 2);

        Count = count;
        Sides = sides;
    }
}
