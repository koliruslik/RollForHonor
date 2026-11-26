namespace RollForHonor.Domain.Rewards;

/// <summary>Maps one effective d100 result to an inclusive integer gold range.</summary>
public sealed record GoldRange
{
    /// <summary>Gets the positive amount awarded on an effective 1.</summary>
    public long Minimum { get; }

    /// <summary>Gets the amount awarded on an effective 100.</summary>
    public long Maximum { get; }

    /// <summary>Defines one gold entry's inclusive range.</summary>
    /// <param name="minimum">The positive lower bound.</param>
    /// <param name="maximum">The upper bound, at least the minimum.</param>
    /// <exception cref="ArgumentOutOfRangeException">The bounds are not positive and ordered.</exception>
    public GoldRange(long minimum, long maximum)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(minimum);
        ArgumentOutOfRangeException.ThrowIfLessThan(maximum, minimum);
        Minimum = minimum;
        Maximum = maximum;
    }

    /// <summary>Applies min + floor((max - min) * (effectiveRoll - 1) / 99) without fractional gold.</summary>
    /// <param name="effectiveRoll">The Luck-adjusted d100 result, from 1 through 100.</param>
    /// <returns>A whole gold amount from the minimum through the maximum.</returns>
    /// <exception cref="ArgumentOutOfRangeException">The effective result is outside 1 through 100.</exception>
    public long AmountAt(decimal effectiveRoll)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(effectiveRoll, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(effectiveRoll, 100);

        return checked(Minimum + (long)decimal.Floor(((decimal)Maximum - Minimum) * (effectiveRoll - 1) / 99));
    }
}
