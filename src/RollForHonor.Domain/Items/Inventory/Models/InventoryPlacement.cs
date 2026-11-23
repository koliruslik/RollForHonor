using RollForHonor.Domain.Items.Models;

namespace RollForHonor.Domain.Items.Inventory.Models;

/// <summary>Captures one item's identity, dimensions and validated top-left cell.</summary>
public sealed class InventoryPlacement
{
    /// <summary>Gets the stable item instance identifier.</summary>
    public string ItemId { get; }

    /// <summary>Gets the immutable occupied dimensions.</summary>
    public ItemFootprint Footprint { get; }

    /// <summary>Gets the top-left occupied cell.</summary>
    public InventoryPosition Position { get; }

    internal InventoryPlacement(string itemId, ItemFootprint footprint, InventoryPosition position)
    {
        ItemId = itemId;
        Footprint = footprint;
        Position = position;
    }
}
