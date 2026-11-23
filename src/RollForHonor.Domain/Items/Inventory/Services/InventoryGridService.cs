using RollForHonor.Domain.Items.Inventory.Models;
using RollForHonor.Domain.Items.Models;

namespace RollForHonor.Domain.Items.Inventory.Services;

/// <summary>Places rectangular items without depending on rendering, ownership storage or combat state.</summary>
public sealed class InventoryGridService
{
    /// <summary>Checks bounds and intersections while ignoring this instance's previous footprint.</summary>
    /// <param name="layout">The immutable current grid.</param>
    /// <param name="itemId">The nonblank instance identifier.</param>
    /// <param name="footprint">The item's positive dimensions.</param>
    /// <param name="position">The candidate top-left cell.</param>
    /// <returns>True if the complete rectangle fits without covering another item.</returns>
    /// <exception cref="ArgumentNullException">The layout or footprint is null.</exception>
    /// <exception cref="ArgumentException">The identifier is blank.</exception>
    public bool CanPlace(InventoryGridLayout layout, string itemId, ItemFootprint footprint, InventoryPosition position)
    {
        ArgumentNullException.ThrowIfNull(layout);
        ArgumentNullException.ThrowIfNull(footprint);
        ArgumentException.ThrowIfNullOrWhiteSpace(itemId);

        return Fits(layout, footprint, position, itemId);
    }

    /// <summary>Creates a placed or moved snapshot, retaining the original object on rejection or no-op.</summary>
    /// <param name="layout">The immutable current grid.</param>
    /// <param name="itemId">The nonblank instance identifier.</param>
    /// <param name="footprint">The item's dimensions.</param>
    /// <param name="position">The candidate top-left cell.</param>
    /// <param name="updated">The valid result, or the original layout when rejected.</param>
    /// <returns>True if placement is valid, including an unchanged position.</returns>
    /// <exception cref="ArgumentNullException">The layout or footprint is null.</exception>
    /// <exception cref="ArgumentException">The identifier is blank.</exception>
    public bool TryPlace(InventoryGridLayout layout, string itemId, ItemFootprint footprint, InventoryPosition position, out InventoryGridLayout updated)
    {
        updated = layout;

        if (!CanPlace(layout, itemId, footprint, position))
        {
            return false;
        }

        if (layout.Placements.TryGetValue(itemId, out var previous) && previous.Position == position && previous.Footprint == footprint)
        {
            return true;
        }

        updated = new InventoryGridLayout(layout.Columns, layout.Rows, layout.Entries.SetItem(itemId, new InventoryPlacement(itemId, footprint, position)));

        return true;
    }

    /// <summary>Finds the first free rectangle in row-major order.</summary>
    /// <param name="layout">The current grid.</param>
    /// <param name="footprint">The requested dimensions.</param>
    /// <param name="position">The selected cell, valid only when true is returned.</param>
    /// <returns>False if no complete rectangle fits.</returns>
    /// <exception cref="ArgumentNullException">The layout or footprint is null.</exception>
    public bool TryFindSpace(InventoryGridLayout layout, ItemFootprint footprint, out InventoryPosition position)
    {
        ArgumentNullException.ThrowIfNull(layout);
        ArgumentNullException.ThrowIfNull(footprint);
        position = default;

        for (var row = 0; row <= layout.Rows - footprint.Height; row++)
        {
            for (var column = 0; column <= layout.Columns - footprint.Width; column++)
            {
                var candidate = new InventoryPosition(column, row);

                if (Fits(layout, footprint, candidate, null))
                {
                    position = candidate;
                    return true;
                }
            }
        }

        return false;
    }

    /// <summary>Removes one placement without changing other positions.</summary>
    /// <param name="layout">The current grid.</param>
    /// <param name="itemId">The nonblank instance identifier.</param>
    /// <returns>The reduced snapshot, or the original when absent.</returns>
    /// <exception cref="ArgumentNullException">The layout is null.</exception>
    /// <exception cref="ArgumentException">The identifier is blank.</exception>
    public InventoryGridLayout Remove(InventoryGridLayout layout, string itemId)
    {
        ArgumentNullException.ThrowIfNull(layout);
        ArgumentException.ThrowIfNullOrWhiteSpace(itemId);

        return layout.Placements.ContainsKey(itemId)
            ? new InventoryGridLayout(layout.Columns, layout.Rows, layout.Entries.Remove(itemId))
            : layout;
    }

    private static bool Fits(InventoryGridLayout layout, ItemFootprint footprint, InventoryPosition position, string? ignored)
    {
        if (position.Column < 0 || position.Row < 0 || position.Column > layout.Columns - footprint.Width || position.Row > layout.Rows - footprint.Height)
        {
            return false;
        }

        return !layout.Placements.Values.Any(other => other.ItemId != ignored
            && position.Column < other.Position.Column + other.Footprint.Width
            && position.Column + footprint.Width > other.Position.Column
            && position.Row < other.Position.Row + other.Footprint.Height
            && position.Row + footprint.Height > other.Position.Row);
    }
}
