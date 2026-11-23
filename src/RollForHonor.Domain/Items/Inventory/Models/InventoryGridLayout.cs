using System.Collections.Immutable;

namespace RollForHonor.Domain.Items.Inventory.Models;

/// <summary>An immutable grid snapshot; the placement service preserves bounds and nonoverlap.</summary>
public sealed class InventoryGridLayout
{
    /// <summary>Gets the positive number of grid columns.</summary>
    public int Columns { get; }

    /// <summary>Gets the positive number of grid rows.</summary>
    public int Rows { get; }

    /// <summary>Gets placements keyed by unique instance identity.</summary>
    public IReadOnlyDictionary<string, InventoryPlacement> Placements => Entries;

    internal ImmutableDictionary<string, InventoryPlacement> Entries { get; }

    /// <summary>Creates an empty grid of the supplied dimensions.</summary>
    /// <param name="columns">The positive column count.</param>
    /// <param name="rows">The positive row count.</param>
    /// <exception cref="ArgumentOutOfRangeException">Either dimension is not positive.</exception>
    public InventoryGridLayout(int columns, int rows)
        : this(columns, rows, ImmutableDictionary<string, InventoryPlacement>.Empty)
    {
    }

    internal InventoryGridLayout(int columns, int rows, ImmutableDictionary<string, InventoryPlacement> entries)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(columns, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(rows, 1);

        Columns = columns;
        Rows = rows;
        Entries = entries;
    }
}
