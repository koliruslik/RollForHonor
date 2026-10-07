using RollForHonor.Domain.Dice.Models;

namespace RollForHonor.Domain.Dice.Services;

/// <summary>
/// Provides dice outcomes and records them for deterministic diagnostics.
/// </summary>
public interface IDiceRoller
{
    /// <summary>
    /// Rolls the supplied dice formula.
    /// </summary>
    DiceRoll Roll(DiceFormula formula);

    /// <summary>
    /// Gets the rolls produced for the current resolution.
    /// </summary>
    DiceTrace Trace { get; }
}
