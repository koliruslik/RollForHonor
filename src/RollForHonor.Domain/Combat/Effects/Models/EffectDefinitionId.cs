namespace RollForHonor.Domain.Combat.Effects.Models;

/// <summary>
/// Identifies an effect definition independently of its runtime instances.
/// </summary>
public sealed record EffectDefinitionId
{
    /// <summary>Gets the underlying identifier value.</summary>
    public Guid Value { get; }

    /// <summary>
    /// Creates a non-empty effect definition identifier.
    /// </summary>
    /// <param name="value">The underlying non-empty GUID.</param>
    /// <exception cref="ArgumentException"><paramref name="value"/> is empty.</exception>
    public EffectDefinitionId(Guid value)
    {
        if (value == Guid.Empty)
        {
            throw new ArgumentException(
                "An effect definition identifier cannot be empty.",
                nameof(value));
        }

        Value = value;
    }

    /// <inheritdoc />
    public override string ToString()
    {
        return Value.ToString("D");
    }
}
