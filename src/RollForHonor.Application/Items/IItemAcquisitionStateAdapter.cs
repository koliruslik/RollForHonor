using RollForHonor.Application.Equipment.Ports;
using RollForHonor.Domain.Combat.State.Models;
using RollForHonor.Domain.Items.Models;

namespace RollForHonor.Application.Items;

/// <summary>Publishes item ownership at the equipment and combat session's version boundary.</summary>
public interface IItemAcquisitionStateAdapter : IEquipmentStateAdapter
{
    /// <summary>Grants an unowned instance only while the expected version and living owner remain current.</summary>
    /// <param name="owner">The registered stat-managed recipient.</param>
    /// <param name="item">The captured item; neither identity nor rolls may change during publication.</param>
    /// <param name="expectedVersion">The version whose inventory was checked for capacity.</param>
    /// <returns>Acquired, AlreadyOwned, VersionConflict or OwnerDefeated without partial changes.</returns>
    /// <exception cref="ArgumentNullException">An input is null.</exception>
    /// <exception cref="KeyNotFoundException">The owner is not registered.</exception>
    /// <exception cref="OverflowException">The session version cannot advance.</exception>
    ItemAcquisitionStatus TryGrantItem(CombatantId owner, ItemInstance item, long expectedVersion);
}
