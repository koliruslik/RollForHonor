using RollForHonor.Domain.Dice.Models;
using RollForHonor.Domain.Dice.Services;

namespace RollForHonor.Infrastructure.Dice;

/// <summary>Rolls runtime dice through .NET randomness with an independent trace per instance.</summary>
public sealed class RandomDiceRoller : IDiceRoller
{
    private readonly Random _random;
    private readonly List<DiceRoll> _rolls = [];

    /// <summary>Creates a fresh dice source without seeding/resetting the shared random stream.</summary>
    /// <param name="random">Optional random stream; absent uses thread-safe Random.Shared. A supplied stream's concurrency belongs to its owner.</param>
    public RandomDiceRoller(Random? random = null) => _random = random ?? Random.Shared;

    /// <inheritdoc />
    public DiceTrace Trace => new(_rolls);

    /// <inheritdoc />
    /// <exception cref="ArgumentNullException">The formula is null.</exception>
    /// <exception cref="OverflowException">The sum cannot be represented by the Domain integer total.</exception>
    public DiceRoll Roll(DiceFormula formula)
    {
        ArgumentNullException.ThrowIfNull(formula);
        var results = Enumerable.Range(0, formula.Count)
            .Select(_ => checked((int)_random.NextInt64(1, (long)formula.Sides + 1))).ToArray();
        var roll = new DiceRoll(formula, results, results.Sum());
        _rolls.Add(roll);
        return roll;
    }
}
