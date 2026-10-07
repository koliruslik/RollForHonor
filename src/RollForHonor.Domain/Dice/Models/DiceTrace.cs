namespace RollForHonor.Domain.Dice.Models;

/// <summary>
/// Records every dice roll performed during one resolution.
/// </summary>
public sealed record DiceTrace
{
    /// <summary>Gets the ordered rolls recorded by the dice source.</summary>
    public IReadOnlyList<DiceRoll> Rolls { get; }

    /// <summary>
    /// Creates an immutable trace from the supplied rolls.
    /// </summary>
    /// <param name="rolls">The rolls in their execution order.</param>
    /// <exception cref="ArgumentNullException"><paramref name="rolls"/> is null.</exception>
    /// <exception cref="ArgumentException"><paramref name="rolls"/> contains a null item.</exception>
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
