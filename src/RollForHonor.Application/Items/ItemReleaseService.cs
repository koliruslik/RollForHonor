using RollForHonor.Application.Equipment.Models;
using RollForHonor.Domain.Combat.State.Models;
using RollForHonor.Domain.Items.Models;
using RollForHonor.Domain.Items.Equipment.Services;
using RollForHonor.Domain.Items.Inventory.Models;
using RollForHonor.Domain.Items.Inventory.Services;

namespace RollForHonor.Application.Items;

/// <summary>Coordinates version-checked release of bag or equipped items into the world.</summary>
public sealed class ItemReleaseService
{
    private readonly IItemReleaseStateAdapter _state;

    /// <summary>Uses the session ownership port.</summary>
    /// <param name="state">The atomic release port.</param>
    /// <exception cref="ArgumentNullException">The port is null.</exception>
    public ItemReleaseService(IItemReleaseStateAdapter state)
    {
        ArgumentNullException.ThrowIfNull(state);
        _state = state;
    }

    /// <summary>Releases an owned item, making room for equipment that must return to the bag.</summary>
    /// <param name="owner">The registered owner.</param>
    /// <param name="itemId">The instance to release.</param>
    /// <param name="layout">The current fixed bag layout, required when dependent equipment returns.</param>
    /// <returns>The released item, or null when release or dependent-item placement is refused.</returns>
    /// <exception cref="ArgumentNullException">The owner is null.</exception>
    /// <exception cref="ArgumentException">The item identity is blank.</exception>
    /// <exception cref="KeyNotFoundException">The owner is not registered.</exception>
    /// <exception cref="InvalidOperationException">A supplied bag layout omits or misplaces owned bag items.</exception>
    /// <exception cref="OverflowException">The session version cannot advance.</exception>
    public ItemInstance? Release(CombatantId owner, string itemId, InventoryGridLayout? layout = null)
    {
        ArgumentNullException.ThrowIfNull(owner);
        ArgumentException.ThrowIfNullOrWhiteSpace(itemId);
        var read = _state.ReadEquipment(owner);

        if (!read.OwnedItems.ContainsKey(itemId))
        {
            return null;
        }

        var slot = read.Loadout.Slots.FirstOrDefault(pair => pair.Value.Id == itemId);

        if (slot.Value is not null)
        {
            var proposed = new EquipmentLoadoutService(read.Rules).Unequip(read.Loadout, slot.Key);
            var retained = proposed.Slots.Values
                .Select(item => item.Id)
                .ToHashSet(StringComparer.Ordinal);
            var returning = read.Loadout.Slots.Values
                .Where(item => item.Id != itemId && !retained.Contains(item.Id))
                .ToArray();

            if (returning.Length > 0 && (layout is null || !CanReturnItems(read, layout, returning)))
            {
                return null;
            }
        }

        return _state.TryReleaseItem(owner, itemId, read.Version);
    }

    private static bool CanReturnItems(EquipmentStateRead read,
        InventoryGridLayout layout, ItemInstance[] returning)
    {
        ItemBagLayout.Validate(read, layout);
        var grid = new InventoryGridService();

        foreach (var item in returning)
        {
            if (!grid.TryFindSpace(layout, item.Base.Footprint, out var position)
                || !grid.TryPlace(layout, item.Id, item.Base.Footprint, position, out layout))
            {
                return false;
            }
        }

        return true;
    }
}
