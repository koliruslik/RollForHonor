using RollForHonor.Domain.Items.Models;
using RollForHonor.Domain.Stats;

namespace RollForHonor.Domain.Items.Affixes.Models;

/// <summary>Defines one tier's numeric interval without assigning a selection weight or power cost.</summary>
public sealed class AffixModifierRange
{
    /// <summary>Gets the affected stat, qualified by damage type when needed.</summary>
    public StatKey Key { get; }

    /// <summary>Gets the stacking operation.</summary>
    public StatOperation Operation { get; }

    /// <summary>Gets the inclusive low-quality endpoint.</summary>
    public decimal Minimum { get; }

    /// <summary>Gets the inclusive high-quality endpoint.</summary>
    public decimal Maximum { get; }

    /// <summary>Gets the retained decimal precision for generated values.</summary>
    public int DecimalPlaces { get; }

    /// <summary>Creates an interval with endpoints representable at the requested precision.</summary>
    /// <param name="key">The affected stat.</param>
    /// <param name="operation">The stacking operation.</param>
    /// <param name="minimum">The inclusive minimum, no greater than maximum.</param>
    /// <param name="maximum">The inclusive maximum.</param>
    /// <param name="decimalPlaces">Precision between zero and six decimal places.</param>
    /// <exception cref="ArgumentException">Endpoints or stat operations are incompatible.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Precision is invalid or a More endpoint is below -1.</exception>
    public AffixModifierRange(StatKey key, StatOperation operation, decimal minimum, decimal maximum, int decimalPlaces = 0)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(decimalPlaces);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(decimalPlaces, 6);

        if (minimum > maximum || decimal.Round(minimum, decimalPlaces) != minimum
            || decimal.Round(maximum, decimalPlaces) != maximum)
        {
            throw new ArgumentException("Range endpoints must be ordered and fit the configured precision.");
        }

        _ = new ItemModifier(key, operation, minimum);
        _ = new ItemModifier(key, operation, maximum);

        Key = key;
        Operation = operation;
        Minimum = minimum;
        Maximum = maximum;
        DecimalPlaces = decimalPlaces;
    }
}
