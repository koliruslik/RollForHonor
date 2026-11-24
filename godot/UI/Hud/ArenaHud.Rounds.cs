using RollForHonor.Domain.Items.Inventory.Models;
using RollForHonor.Application.Items;
using RollForHonor.Domain.Items.Models;

namespace RollForHonor.Godot.UI.Hud;

public partial class ArenaHud
{
    /// <summary>Gets the selected nearby-menu item when that menu is available.</summary>
    public string SelectedLootId => _rounds.SelectedLootId;

    /// <summary>Reads the actual fixed bag layout, retaining current player-arranged positions.</summary>
    /// <returns>The layout corresponding to canonical unequipped ownership.</returns>
    public InventoryGridLayout ReadBagLayout() => _items.ReadBagLayout();

    /// <summary>Capacity-checks and publishes one captured pickup against the live bag layout.</summary>
    /// <param name="item">The unowned dropped instance.</param>
    /// <param name="destination">Optional exact top-left bag cell.</param>
    /// <param name="recordAction">Whether this acquisition represents a world pickup in the journal.</param>
    /// <returns>The acquisition result; rejection leaves the item in the world.</returns>
    public ItemAcquisitionStatus AcquireItem(ItemInstance item, InventoryPosition? destination = null, bool recordAction = true)
        => _items.AcquireItem(item, destination, recordAction);

    /// <summary>Retains a completed world action alongside combat in the HUD journal.</summary>
    /// <param name="message">The captured translated action message.</param>
    public void RecordAction(string message) => _history.RecordAction(message);

    private void ShowCapacityWarning() => _rounds.ShowCapacityWarning();
}
