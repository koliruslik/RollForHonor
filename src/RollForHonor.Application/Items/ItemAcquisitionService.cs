using RollForHonor.Domain.Combat.State.Models;
using RollForHonor.Domain.Items.Inventory.Models;
using RollForHonor.Domain.Items.Inventory.Services;
using RollForHonor.Domain.Items.Models;

namespace RollForHonor.Application.Items;

/// <summary>Checks a canonical twelve-by-five bag before atomically granting a captured world item.</summary>
public sealed class ItemAcquisitionService
{
    private readonly IItemAcquisitionStateAdapter _state;

    /// <summary>Uses the session's shared ownership and version port.</summary>
    /// <param name="state">The atomic acquisition port.</param>
    /// <exception cref="ArgumentNullException">The port is null.</exception>
    public ItemAcquisitionService(IItemAcquisitionStateAdapter state)
    {
        ArgumentNullException.ThrowIfNull(state);
        _state = state;
    }

    /// <summary>Validates every existing placement and attempts one grant without rearranging other items or rerolling loot.</summary>
    /// <param name="owner">The stat-managed recipient.</param>
    /// <param name="item">The immutable dropped instance.</param>
    /// <param name="layout">The current bag layout, excluding equipped items.</param>
    /// <returns>The capacity, ownership or atomic publication result.</returns>
    /// <exception cref="ArgumentNullException">An input is null.</exception>
    /// <exception cref="InvalidOperationException">The layout does not exactly represent the owner's current bag.</exception>
    /// <exception cref="KeyNotFoundException">The owner is not registered.</exception>
    public ItemAcquisitionStatus Acquire(CombatantId owner, ItemInstance item, InventoryGridLayout layout)
    {
        ArgumentNullException.ThrowIfNull(owner);
        ArgumentNullException.ThrowIfNull(item);
        ArgumentNullException.ThrowIfNull(layout);
        var read = _state.ReadEquipment(owner);

        if (read.OwnedItems.ContainsKey(item.Id))
        {
            return ItemAcquisitionStatus.AlreadyOwned;
        }

        var equipped = read.Loadout.Slots.Values
            .Select(value => value.Id)
            .ToHashSet(StringComparer.Ordinal);
        var bag = read.OwnedItems.Values
            .Where(value => !equipped.Contains(value.Id))
            .ToDictionary(value => value.Id, StringComparer.Ordinal);
        var placement = new InventoryGridService();

        if (layout.Columns != 12 || layout.Rows != 5 || layout.Placements.Count != bag.Count
            || layout.Placements.Values.Any(entry => !bag.TryGetValue(entry.ItemId, out var existing)
                || entry.Footprint != existing.Base.Footprint
                || !placement.CanPlace(layout, entry.ItemId, entry.Footprint, entry.Position)))
        {
            throw new InvalidOperationException("The pickup layout must represent every unequipped owned item in the fixed bag.");
        }

        if (!placement.TryFindSpace(layout, item.Base.Footprint, out _))
        {
            return ItemAcquisitionStatus.InventoryFull;
        }

        return _state.TryGrantItem(owner, item, read.Version);
    }
}
