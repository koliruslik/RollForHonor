using RollForHonor.Domain.Items.Inventory.Models;
using RollForHonor.Domain.Items.Models;

namespace RollForHonor.Domain.Items.Inventory.Services;

/// <summary>Reconciles fixed bag contents while retaining existing rectangles and preferred positions.</summary>
public sealed class InventoryLayoutService
{
    /// <summary>Places all incoming items without moving retained items or expanding the grid.</summary>
    /// <param name="layout">The current immutable grid.</param>
    /// <param name="items">Canonical incoming bag items with unique identities.</param>
    /// <param name="updated">The complete layout on success; otherwise the original layout.</param>
    /// <param name="preferred">Optional preferred cells for newly returning items.</param>
    /// <returns>False when any item cannot fit.</returns>
    /// <exception cref="ArgumentNullException">The layout or items are null.</exception>
    /// <exception cref="ArgumentException">Items are null, oversized or have duplicate identities.</exception>
    public bool TryReconcile(InventoryGridLayout layout, IReadOnlyList<ItemInstance> items,
        out InventoryGridLayout updated, IReadOnlyDictionary<string, InventoryPosition>? preferred = null)
    {
        ArgumentNullException.ThrowIfNull(layout);
        ArgumentNullException.ThrowIfNull(items);

        if (items.Any(item => item is null || item.Base.Footprint.Width > layout.Columns || item.Base.Footprint.Height > layout.Rows))
        {
            throw new ArgumentException("Inventory items must be nonnull and fit the fixed grid dimensions.", nameof(items));
        }

        var incoming = items.ToDictionary(item => item.Id, StringComparer.Ordinal);
        var placement = new InventoryGridService();
        var candidate = layout;

        foreach (var entry in layout.Placements.Values.Where(entry => !incoming.ContainsKey(entry.ItemId)))
        {
            candidate = placement.Remove(candidate, entry.ItemId);
        }

        foreach (var item in items
            .OrderByDescending(item => item.Base.Footprint.Height)
            .ThenByDescending(item => item.Base.Footprint.Width)
            .ThenBy(item => item.Id, StringComparer.Ordinal))
        {
            if (candidate.Placements.TryGetValue(item.Id, out var existing) && existing.Footprint == item.Base.Footprint)
            {
                continue;
            }

            candidate = placement.Remove(candidate, item.Id);

            if (preferred is not null && preferred.TryGetValue(item.Id, out var preferredCell)
                && placement.TryPlace(candidate, item.Id, item.Base.Footprint, preferredCell, out var preferredLayout))
            {
                candidate = preferredLayout;
                continue;
            }

            if (!placement.TryFindSpace(candidate, item.Base.Footprint, out var position)
                || !placement.TryPlace(candidate, item.Id, item.Base.Footprint, position, out candidate))
            {
                updated = layout;
                return false;
            }
        }

        updated = candidate;
        return true;
    }
}
