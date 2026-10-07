namespace RollForHonor.Domain.Dice.Models;

/// <summary>
/// Defines the number and size of dice in a roll.
/// </summary>
public sealed record DiceFormula
{
    /// <summary>Gets the number of dice.</summary>
    public int Count { get; }

    /// <summary>Gets the number of sides on each die.</summary>
    public int Sides { get; }

    /// <summary>
    /// Creates a validated dice formula.
    /// </summary>
    public DiceFormula(int count, int sides)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(count, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(sides, 2);

        Count = count;
        Sides = sides;
    }

    public override string ToString()
    {
        return $"{Count}d{Sides}";
    }
}
