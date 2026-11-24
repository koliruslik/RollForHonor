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
    /// <param name="destination">Optional exact top-left cell; omitted uses the first fitting rectangle.</param>
    /// <returns>The capacity, ownership or atomic publication result.</returns>
    /// <exception cref="ArgumentNullException">An input is null.</exception>
    /// <exception cref="InvalidOperationException">The layout does not exactly represent the owner's current bag.</exception>
    /// <exception cref="KeyNotFoundException">The owner is not registered.</exception>
    public ItemAcquisitionStatus Acquire(CombatantId owner, ItemInstance item, InventoryGridLayout layout, InventoryPosition? destination = null)
    {
        ArgumentNullException.ThrowIfNull(owner);
        ArgumentNullException.ThrowIfNull(item);
        ArgumentNullException.ThrowIfNull(layout);
        var read = _state.ReadEquipment(owner);

        if (read.OwnedItems.ContainsKey(item.Id))
        {
            return ItemAcquisitionStatus.AlreadyOwned;
        }

        ItemBagLayout.Validate(read, layout);
        var placement = new InventoryGridService();

        if (destination is { } position
            ? !placement.CanPlace(layout, item.Id, item.Base.Footprint, position)
            : !placement.TryFindSpace(layout, item.Base.Footprint, out _))
        {
            return ItemAcquisitionStatus.InventoryFull;
        }

        return _state.TryGrantItem(owner, item, read.Version);
    }
}
