namespace RollForHonor.Domain.Rewards;

/// <summary>Captures the agreed Luck policy for affix tiers and d100 quality/amount rolls.</summary>
public sealed record GenerationLuck
{
    /// <summary>Gets the captured Luck value clamped upward to zero.</summary>
    public decimal Value { get; }

    /// <summary>Gets min(5, floor(Luck / 5)), applied only to affix-tier d20 rolls.</summary>
    public int AffixTierBonus => Value >= 25 ? 5 : (int)decimal.Floor(Value / 5);

    /// <summary>Captures Luck without changing weights, affix count or group-selection policy.</summary>
    /// <param name="luck">The character Luck snapshot; negative values behave as zero.</param>
    public GenerationLuck(decimal luck) => Value = Math.Max(0, luck);

    /// <summary>Adds captured Luck to a natural d100 and caps the effective result at 100.</summary>
    /// <param name="naturalRoll">The unmodified face from 1 through 100.</param>
    /// <returns>The exact effective result, retaining fractional Luck without intermediate rounding.</returns>
    /// <exception cref="ArgumentOutOfRangeException">The face is outside 1 through 100.</exception>
    public decimal BoostD100(int naturalRoll)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(naturalRoll, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(naturalRoll, 100);

        return Value >= 100 - naturalRoll ? 100 : naturalRoll + Value;
    }
}
