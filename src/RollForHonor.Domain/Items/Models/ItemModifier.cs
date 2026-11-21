using RollForHonor.Domain.Stats;

namespace RollForHonor.Domain.Items.Models;

/// <summary>Stores a concrete item modifier before an owned item/affix supplies its source identifier.</summary>
public sealed record ItemModifier
{
    /// <summary>Gets the affected scalar or damage-qualified stat.</summary>
    public StatKey Key { get; }

    /// <summary>Gets the flat, additive percentage or independent percentage operation.</summary>
    public StatOperation Operation { get; }

    /// <summary>Gets an absolute amount, or a fraction for percentages and flat resistances; flat resistance 0.1 adds ten percentage points.</summary>
    public decimal Amount { get; }

    /// <summary>Creates a stored value using the existing stat arithmetic constraints.</summary>
    /// <param name="key">The affected stat.</param>
    /// <param name="operation">The stacking operation.</param>
    /// <param name="amount">The absolute or fractional amount.</param>
    /// <exception cref="ArgumentException">Flat damage-coefficient modification is requested.</exception>
    /// <exception cref="ArgumentOutOfRangeException">The operation is invalid or More is below -1.</exception>
    public ItemModifier(StatKey key, StatOperation operation, decimal amount)
    {
        var validated = new StatModifier(key, operation, amount, "item-value");
        Key = validated.Key;
        Operation = validated.Operation;
        Amount = validated.Amount;
    }
}
