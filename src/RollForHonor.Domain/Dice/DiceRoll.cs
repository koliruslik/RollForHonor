namespace RollForHonor.Domain.Dice;

public sealed record DiceRoll
{
    public DiceFormula Formula { get; }

    public IReadOnlyList<int> Results { get; }

    public int Total { get; }

    public DiceRoll(
        DiceFormula formula,
        IReadOnlyList<int> results,
        int total)
    {
        ArgumentNullException.ThrowIfNull(formula);
        ArgumentNullException.ThrowIfNull(results);

        if (results.Count != formula.Count)
        {
            throw new ArgumentException(
                "The number of results must match the dice count.",
                nameof(results));
        }

        if (results.Any(result => result < 1 || result > formula.Sides))
        {
            throw new ArgumentOutOfRangeException(
                nameof(results),
                "Every result must be within the dice range.");
        }

        if (results.Sum() != total)
        {
            throw new ArgumentException(
                "The total must equal the sum of all dice results.",
                nameof(total));
        }

        Formula = formula;
        Results = results.ToArray();
        Total = total;
    }
}
