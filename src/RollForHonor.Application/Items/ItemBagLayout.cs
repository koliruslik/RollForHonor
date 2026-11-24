using RollForHonor.Application.Equipment.Models;
using RollForHonor.Domain.Items.Inventory.Models;
using RollForHonor.Domain.Items.Inventory.Services;

namespace RollForHonor.Application.Items;

internal static class ItemBagLayout
{
    internal static void Validate(EquipmentStateRead read, InventoryGridLayout layout)
    {
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
            throw new InvalidOperationException("The layout must represent every unequipped owned item in the fixed bag.");
        }
    }
}
