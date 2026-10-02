namespace RollForHonor.Domain.Dice;

/// <summary>
/// Captures the individual results and total of one dice roll.
/// </summary>
public sealed record DiceRoll
{
    /// <summary>Gets the formula used for the roll.</summary>
    public DiceFormula Formula { get; }

    /// <summary>Gets the result of each individual die.</summary>
    public IReadOnlyList<int> Results { get; }

    /// <summary>Gets the sum of all dice results.</summary>
    public int Total { get; }

    /// <summary>
    /// Creates a validated result for the supplied formula.
    /// </summary>
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
