using RollForHonor.Domain.Dice.Models;

namespace RollForHonor.Domain.Dice.Services;

/// <summary>
/// Provides dice outcomes and records them for deterministic diagnostics.
/// </summary>
public interface IDiceRoller
{
    /// <summary>
    /// Rolls every die in the supplied formula and appends the result to <see cref="Trace"/>.
    /// </summary>
    /// <param name="formula">The number and size of dice to roll.</param>
    /// <returns>The individual die results and their total.</returns>
    DiceRoll Roll(DiceFormula formula);

    /// <summary>
    /// Gets the rolls produced for the current resolution.
    /// </summary>
    DiceTrace Trace { get; }
}
