using RollForHonor.Application.Equipment.Ports;
using RollForHonor.Domain.Combat.State.Models;
using RollForHonor.Domain.Items.Models;

namespace RollForHonor.Application.Items;

/// <summary>Releases bag ownership atomically while preserving the captured item.</summary>
public interface IItemReleaseStateAdapter : IEquipmentStateAdapter
{
    /// <summary>Removes one unequipped item while the living owner and expected version remain current.</summary>
    /// <param name="owner">The registered owner.</param>
    /// <param name="itemId">The owned bag instance identity.</param>
    /// <param name="expectedVersion">The checked ownership version.</param>
    /// <returns>The released canonical instance, or null when stale, absent, equipped or defeated.</returns>
    /// <exception cref="ArgumentNullException">The owner is null.</exception>
    /// <exception cref="ArgumentException">The item identity is blank.</exception>
    /// <exception cref="KeyNotFoundException">The owner is not registered.</exception>
    /// <exception cref="OverflowException">The session version cannot advance.</exception>
    ItemInstance? TryReleaseItem(CombatantId owner, string itemId, long expectedVersion);
}
