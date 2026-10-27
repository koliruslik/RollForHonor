using RollForHonor.Domain.Combat.Resolution.Models;
using RollForHonor.Domain.Dice.Services;

namespace RollForHonor.Domain.Combat.Effects.Services;

/// <summary>
/// Defines behavior explicitly scheduled by composition, independently of status application.
/// Implementations receive runtime inputs here and calculation dependencies through their constructors.
/// </summary>
public interface ICombatEffect
{
    /// <summary>Gets the diagnostic name used in nested execution traces.</summary>
    string Name => GetType().Name;

    /// <summary>Calculates effect behavior at its configured position in the pipeline.</summary>
    /// <param name="context">Current source, target, rolls and optional damage results.</param>
    /// <param name="dice">The same dice source used by the rest of this action.</param>
    /// <returns>A new context with preserved prior results and any additional proposals.</returns>
    /// <remarks>
    /// An inapplicable effect may return the same context. Never mutate snapshots, commit state,
    /// finalize health, or automatically process newly proposed statuses. The composer selects
    /// which pre-existing effects run and when. Direct health loss may return a negative adjustment;
    /// healing bonuses and tick scheduling belong to their separate calculations.
    /// </remarks>
    CombatContext Apply(CombatContext context, IDiceRoller dice);
}
