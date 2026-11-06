using RollForHonor.Domain.Combat.Resolution.Models;
using RollForHonor.Domain.Dice.Services;

namespace RollForHonor.Domain.Combat.Resolution.Services;

/// <summary>A composable calculation that returns a new context and never commits live state.</summary>
public interface ICombatStep
{
    /// <summary>Gets the human-readable execution trace name.</summary>
    string Name => GetType().Name;

    /// <summary>Executes once using the action's shared dice source.</summary>
    /// <param name="context">Current immutable inputs and results.</param>
    /// <param name="dice">The shared deterministic dice source.</param>
    /// <returns>A non-null context retaining the original request and prior results.</returns>
    /// <exception cref="ArgumentNullException">An argument is null.</exception>
    /// <exception cref="InvalidOperationException">Required preceding results are absent or the stage was already completed.</exception>
    CombatContext Execute(CombatContext context, IDiceRoller dice);
}
