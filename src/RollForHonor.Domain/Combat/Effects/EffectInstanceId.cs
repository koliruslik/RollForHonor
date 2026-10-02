namespace RollForHonor.Domain.Combat.Effects;

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
}
