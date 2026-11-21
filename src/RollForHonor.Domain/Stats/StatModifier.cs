namespace RollForHonor.Domain.Stats;

/// <summary>Determines how an attribute modifier participates in calculation.</summary>
public enum StatOperation
{
    /// <summary>Add to the base value before percentage scaling.</summary>
    Flat,

    /// <summary>Add a fractional increase; negative values reduce the additive pool.</summary>
    Increased,

    /// <summary>Apply an independent fractional multiplier; -1 means 100% less.</summary>
    More
}

/// <summary>Describes an immutable modifier with an explicit origin, independent of items and Godot.</summary>
public sealed record StatModifier
{
    /// <summary>Gets the affected attribute.</summary>
    public StatKey Key { get; }

    /// <summary>Gets the calculation operation.</summary>
    public StatOperation Operation { get; }

    /// <summary>Gets an absolute flat amount or a fractional percentage, where 0.2 means 20%.</summary>
    public decimal Amount { get; }

    /// <summary>Gets the origin identifier, suitable for a future item or effect instance.</summary>
    public string Source { get; }

    /// <summary>Creates a validated modifier.</summary>
    /// <param name="key">The affected attribute.</param>
    /// <param name="operation">A defined calculation operation.</param>
    /// <param name="amount">The flat amount or fractional increase/more value.</param>
    /// <param name="source">A nonempty origin identifier.</param>
    /// <exception cref="ArgumentException">The origin is blank or flat damage-coefficient modification is requested.</exception>
    /// <exception cref="ArgumentOutOfRangeException">The operation is undefined or a More factor would be negative.</exception>
    public StatModifier(StatKey key, StatOperation operation, decimal amount, string source)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(source);

        if (!Enum.IsDefined(operation) || operation == StatOperation.More && amount < -1)
        {
            throw new ArgumentOutOfRangeException(nameof(operation));
        }

        if (key.Type == StatType.DamageMultiplier && operation == StatOperation.Flat)
        {
            throw new ArgumentException("Use Increased or More for outgoing damage coefficients.", nameof(operation));
        }

        Key = key;
        Operation = operation;
        Amount = amount;
        Source = source;
    }
}
