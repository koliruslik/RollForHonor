using RollForHonor.Domain.Items.Inventory.Models;
using RollForHonor.Domain.Items.Inventory.Services;
using RollForHonor.Domain.Items.Models;

namespace RollForHonor.Domain.Tests.Items.Inventory;

/// <summary>Verifies spatial bounds, immutable moves, rectangle intersections and deterministic first-fit placement.</summary>
public sealed class InventoryGridTests
{
    /// <summary>Base dimensions cannot contain an empty or negative side.</summary>
    /// <param name="width">The candidate width.</param>
    /// <param name="height">The candidate height.</param>
    [Theory]
    [InlineData(0, 2)]
    [InlineData(-1, 2)]
    [InlineData(2, 0)]
    [InlineData(2, -1)]
    public void FootprintsRequirePositiveDimensions(int width, int height)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new ItemFootprint(width, height));
    }

    /// <summary>Bases without explicit sizes remain compatible with existing one-cell content.</summary>
    [Fact]
    public void BaseRetainsItsAuthoredFootprintOrUsesOneCell()
    {
        var size = new ItemFootprint(2, 3);
        var sized = new ItemDefinition("bow", "Bow", footprint: size);

        Assert.Same(size, sized.Footprint);
        Assert.Equal(new ItemFootprint(1, 1), new ItemDefinition("ring", "Ring").Footprint);
    }

    /// <summary>Out-of-bounds candidates preserve the entire previous snapshot.</summary>
    /// <param name="column">The candidate column.</param>
    /// <param name="row">The candidate row.</param>
    [Theory]
    [InlineData(-1, 0)]
    [InlineData(0, -1)]
    [InlineData(3, 0)]
    [InlineData(0, 3)]
    [InlineData(int.MaxValue, 0)]
    public void RejectsWholeRectanglesOutsideTheGrid(int column, int row)
    {
        var service = new InventoryGridService();
        var grid = new InventoryGridLayout(4, 4);

        Assert.False(service.TryPlace(grid, "item", new ItemFootprint(2, 2), new InventoryPosition(column, row), out var rejected));
        Assert.Same(grid, rejected);
        Assert.Empty(grid.Placements);
    }

    /// <summary>Moving one item can overlap its previous cells but cannot cover another item.</summary>
    [Fact]
    public void MovesIgnoreOnlyTheSameInstanceAndPreservePriorSnapshots()
    {
        var service = new InventoryGridService();
        Assert.True(service.TryPlace(new InventoryGridLayout(5, 4), "bow", new ItemFootprint(2, 3), new InventoryPosition(0, 0), out var first));
        Assert.True(service.TryPlace(first, "ring", ItemFootprint.SingleCell, new InventoryPosition(2, 0), out var second));
        Assert.False(service.TryPlace(second, "bow", new ItemFootprint(2, 3), new InventoryPosition(1, 0), out var rejected));
        Assert.Same(second, rejected);
        Assert.True(service.TryPlace(second, "bow", new ItemFootprint(2, 3), new InventoryPosition(1, 1), out var moved));
        Assert.Equal(new InventoryPosition(0, 0), second.Placements["bow"].Position);
        Assert.Equal(new InventoryPosition(1, 1), moved.Placements["bow"].Position);
        Assert.Equal(2, moved.Placements.Count);
        Assert.True(service.TryFindSpace(moved, ItemFootprint.SingleCell, out var free));
        Assert.Equal(new InventoryPosition(0, 0), free);
    }

    /// <summary>Free cell count does not imply that a larger rectangle can fit.</summary>
    [Fact]
    public void FragmentationRequiresContiguousSpace()
    {
        var service = new InventoryGridService();
        Assert.True(service.TryPlace(new InventoryGridLayout(3, 3), "barrier", new ItemFootprint(1, 3), new InventoryPosition(1, 0), out var grid));
        Assert.False(service.TryFindSpace(grid, new ItemFootprint(2, 2), out _));
        Assert.True(service.TryPlace(new InventoryGridLayout(3, 5), "barrier", new ItemFootprint(1, 3), new InventoryPosition(1, 0), out var taller));
        Assert.True(service.TryFindSpace(taller, new ItemFootprint(2, 2), out var free));
        Assert.Equal(new InventoryPosition(0, 3), free);
        Assert.Equal(3, grid.Rows);
    }

    /// <summary>No-op moves and absent removals retain identity; removal releases the full rectangle.</summary>
    [Fact]
    public void NoOpsAndRemovalKeepOtherStateIntact()
    {
        var service = new InventoryGridService();
        var size = new ItemFootprint(2, 2);
        Assert.True(service.TryPlace(new InventoryGridLayout(2, 2), "armor", size, default, out var occupied));
        Assert.True(service.TryPlace(occupied, "armor", new ItemFootprint(2, 2), default, out var same));
        Assert.Same(occupied, same);
        Assert.Same(occupied, service.Remove(occupied, "absent"));
        var empty = service.Remove(occupied, "armor");

        Assert.Single(occupied.Placements);
        Assert.Empty(empty.Placements);
        Assert.True(service.TryFindSpace(empty, size, out var free));
        Assert.Equal(default, free);
    }

    /// <summary>Oversized metadata is rejected without overflowing coordinate arithmetic.</summary>
    [Fact]
    public void OversizedFootprintsAndInvalidDimensionsFailSafely()
    {
        var service = new InventoryGridService();
        var grid = new InventoryGridLayout(4, 4);
        var oversized = new ItemFootprint(int.MaxValue, int.MaxValue);

        Assert.False(service.CanPlace(grid, "huge", oversized, default));
        Assert.False(service.TryFindSpace(grid, oversized, out _));
        Assert.Throws<ArgumentOutOfRangeException>(() => new InventoryGridLayout(0, 4));
        Assert.Throws<ArgumentOutOfRangeException>(() => new InventoryGridLayout(4, 0));
    }
}
