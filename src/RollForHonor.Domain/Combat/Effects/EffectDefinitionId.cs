namespace RollForHonor.Domain.Combat.Effects;

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
}
