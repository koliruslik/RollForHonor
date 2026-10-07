using RollForHonor.Domain.Combat.State.Models;

namespace RollForHonor.Domain.Combat.State.Services;

/// <summary>
/// Projects final health from an atomic collection of health adjustments.
/// </summary>
public interface IHealthStateProjector
{
    /// <summary>
    /// Sums all adjustments for one combatant and clamps projected health to
    /// the inclusive range from zero through maximum health.
    /// </summary>
    /// <param name="combatant">The immutable combatant state being projected.</param>
    /// <param name="adjustments">Signed health changes targeting that combatant.</param>
    /// <returns>The original and projected health without mutating the snapshot.</returns>
    /// <exception cref="ArgumentNullException">A reference argument is null.</exception>
    /// <exception cref="ArgumentException">
    /// An adjustment is null or targets another combatant.
    /// </exception>
    HealthProjection Project(
        CombatantSnapshot combatant,
        IReadOnlyList<HealthAdjustment> adjustments);
}
