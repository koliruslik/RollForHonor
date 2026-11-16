namespace RollForHonor.Domain.Resources.Models;

/// <summary>An exact, nonnegative resource quantity without implicit integer rounding.</summary>
public sealed record ResourceAmount
{
    /// <summary>Gets the resource pool.</summary>
    public ResourceId Resource { get; }
    /// <summary>Gets its exact quantity.</summary>
    public decimal Amount { get; }
    /// <summary>Creates a resource quantity.</summary>
    /// <param name="resource">The non-null pool identifier.</param>
    /// <param name="amount">A nonnegative quantity.</param>
    /// <exception cref="ArgumentNullException">The identifier is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">The quantity is negative.</exception>
    public ResourceAmount(ResourceId resource, decimal amount)
    {
        ArgumentNullException.ThrowIfNull(resource);
        ArgumentOutOfRangeException.ThrowIfNegative(amount);
        Resource = resource;
        Amount = amount;
    }
}
