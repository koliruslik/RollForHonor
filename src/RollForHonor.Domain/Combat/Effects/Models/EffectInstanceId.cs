namespace RollForHonor.Domain.Combat.Effects.Models;

/// <summary>
/// Identifies one applied runtime instance of an effect.
/// </summary>
public sealed record EffectInstanceId
{
    /// <summary>Gets the underlying identifier value.</summary>
    public Guid Value { get; }

    /// <summary>
    /// Creates a non-empty effect instance identifier.
    /// </summary>
    /// <param name="value">The underlying non-empty GUID.</param>
    /// <exception cref="ArgumentException"><paramref name="value"/> is empty.</exception>
    public EffectInstanceId(Guid value)
    {
        if (value == Guid.Empty)
        {
            throw new ArgumentException(
                "An effect instance identifier cannot be empty.",
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
