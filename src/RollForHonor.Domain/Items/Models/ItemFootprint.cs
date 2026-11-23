namespace RollForHonor.Domain.Items.Models;

/// <summary>Defines the rectangular inventory space occupied by an item base.</summary>
public sealed record ItemFootprint
{
    /// <summary>Gets the default footprint for bases without explicit dimensions.</summary>
    public static ItemFootprint SingleCell { get; } = new(1, 1);

    /// <summary>Gets the positive number of occupied columns.</summary>
    public int Width { get; }

    /// <summary>Gets the positive number of occupied rows.</summary>
    public int Height { get; }

    /// <summary>Creates a rectangular footprint measured in inventory cells.</summary>
    /// <param name="width">The positive width.</param>
    /// <param name="height">The positive height.</param>
    /// <exception cref="ArgumentOutOfRangeException">Either dimension is not positive.</exception>
    public ItemFootprint(int width, int height)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(width, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(height, 1);

        Width = width;
        Height = height;
    }
}
