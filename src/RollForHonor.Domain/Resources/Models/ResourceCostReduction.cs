namespace RollForHonor.Domain.Resources.Models;

/// <summary>A supplied percentage cost reduction for one resource, expressed as a fraction.</summary>
public sealed record ResourceCostReduction
{
    /// <summary>Gets the affected resource.</summary>
    public ResourceId Resource { get; }
    /// <summary>Gets the reduction fraction, where 0.1 means ten percent.</summary>
    public decimal Fraction { get; }
    /// <summary>Gets the content-supplied modifier origin, or null when the provider does not identify it.</summary>
    public string? Origin { get; }
    /// <summary>Creates one reduction modifier.</summary>
    /// <param name="resource">The affected pool.</param>
    /// <param name="fraction">A fraction from zero through one.</param>
    /// <param name="origin">Optional diagnostic identity of the supplying effect or item.</param>
    /// <exception cref="ArgumentNullException">The resource is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">The fraction is outside its range.</exception>
    public ResourceCostReduction(ResourceId resource, decimal fraction, string? origin = null)
    {
        ArgumentNullException.ThrowIfNull(resource);
        ArgumentOutOfRangeException.ThrowIfNegative(fraction);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(fraction, 1m);
        Resource = resource;
        Fraction = fraction;
        Origin = origin;
    }
}
