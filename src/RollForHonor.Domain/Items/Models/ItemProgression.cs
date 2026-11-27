namespace RollForHonor.Domain.Items.Models;

/// <summary>Captures enhancement and rarity-base scaling independently of rolled affixes.</summary>
public sealed record ItemProgression
{
    /// <summary>Gets the enhancement level within 0–10.</summary>
    public int Enhancement { get; }

    /// <summary>Gets the captured rarity multiplier relative to the original base.</summary>
    public decimal RarityMultiplier { get; }

    /// <summary>Gets the captured linear increase per enhancement level.</summary>
    public decimal Step { get; }

    /// <summary>Gets the total multiplier applied only to scalable base properties.</summary>
    public decimal Multiplier => RarityMultiplier * (1 + Step * Enhancement);

    /// <summary>Captures progression without modifying the original base snapshot.</summary>
    /// <param name="enhancement">A level within 0–10.</param>
    /// <param name="rarityMultiplier">A positive rarity-base multiplier.</param>
    /// <param name="step">A nonnegative fractional enhancement step.</param>
    /// <exception cref="ArgumentOutOfRangeException">A progression value is invalid.</exception>
    public ItemProgression(int enhancement = 0, decimal rarityMultiplier = 1, decimal step = 0.05m)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(enhancement);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(enhancement, 10);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(rarityMultiplier);
        ArgumentOutOfRangeException.ThrowIfNegative(step);
        Enhancement = enhancement;
        RarityMultiplier = rarityMultiplier;
        Step = step;
    }
}
