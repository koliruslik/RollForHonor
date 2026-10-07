using RollForHonor.Domain.Combat.State.Models;

namespace RollForHonor.Domain.Combat.State.Services;

/// <summary>
/// Projects final health from an atomic collection of health adjustments.
/// </summary>
public interface IHealthStateProjector
{
    /// <summary>
    /// Applies the net adjustment and clamps final health to valid bounds.
    /// </summary>
    HealthProjection Project(
        CombatantSnapshot combatant,
        IReadOnlyList<HealthAdjustment> adjustments);
}
