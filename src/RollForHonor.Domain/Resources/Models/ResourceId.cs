namespace RollForHonor.Domain.Resources.Models;

/// <summary>Identifies a resource pool by a stable, case-sensitive content key.</summary>
public sealed record ResourceId
{
    /// <summary>Gets the resource key.</summary>
    public string Value { get; }
    /// <summary>Creates a resource identifier.</summary>
    /// <param name="value">A nonblank key, for example mana or stamina.</param>
    /// <exception cref="ArgumentException">The key is null or blank.</exception>
    public ResourceId(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        Value = value;
    }
}
