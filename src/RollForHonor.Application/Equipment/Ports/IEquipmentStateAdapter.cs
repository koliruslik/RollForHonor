using RollForHonor.Application.Equipment.Models;
using RollForHonor.Domain.Combat.State.Models;

namespace RollForHonor.Application.Equipment.Ports;

/// <summary>Coordinates equipment ownership and stat publication using the combat session's version boundary.</summary>
public interface IEquipmentStateAdapter
{
    /// <summary>Reads canonical owned items and equipment inputs at one consistent version.</summary>
    /// <param name="ownerId">The stat-managed actor to read.</param>
    /// <returns>An immutable snapshot containing only that actor's owned items.</returns>
    /// <exception cref="ArgumentNullException">The owner is null.</exception>
    /// <exception cref="KeyNotFoundException">The actor is not stat-managed.</exception>
    EquipmentStateRead ReadEquipment(CombatantId ownerId);

    /// <summary>Validates canonical ownership and compatibility, recomposes stats and atomically publishes the whole change.</summary>
    /// <param name="proposal">The complete loadout and its expected session version.</param>
    /// <returns>Only Applied, Unchanged or VersionConflict; unchanged proposals still require a current version.</returns>
    /// <remarks>Every failed/rejected operation preserves equipment, profile, stats, HP, resources, cooldowns and receipts.</remarks>
    /// <exception cref="ArgumentNullException">The proposal is null.</exception>
    /// <exception cref="KeyNotFoundException">The actor is not stat-managed.</exception>
    /// <exception cref="InvalidOperationException">A proposed item is not canonical/owned or violates slot rules.</exception>
    /// <exception cref="OverflowException">Calculation, projection or version arithmetic overflows.</exception>
    EquipmentChangeStatus TryCommitEquipment(EquipmentChangeProposal proposal);
}
