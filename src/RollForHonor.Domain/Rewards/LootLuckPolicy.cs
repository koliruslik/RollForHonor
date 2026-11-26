namespace RollForHonor.Domain.Rewards;

/// <summary>Tilts one uniform loot roll toward better ordered outcomes without additional dice or rerolls.</summary>
public sealed class LootLuckPolicy
{
    /// <summary>Gets the positive Luck amount that adds one unit to the cumulative-distribution exponent.</summary>
    public decimal Scale { get; }

    /// <summary>Gets the integer resolution of the adjusted selection roll.</summary>
    public int Resolution { get; }

    /// <summary>Configures F(luck) = F(base) raised to 1 + max(0, Luck) / scale.</summary>
    /// <param name="scale">Positive balance scale; 100 reproduces the approved occurrence curve.</param>
    /// <param name="resolution">At least two faces; must divide into the base distribution exactly when applied.</param>
    /// <exception cref="ArgumentOutOfRangeException">Scale is not positive or resolution is below two.</exception>
    public LootLuckPolicy(decimal scale, int resolution = 1_000_000)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(scale);
        ArgumentOutOfRangeException.ThrowIfLessThan(resolution, 2);
        Scale = scale;
        Resolution = resolution;
    }

    /// <summary>Builds exact integer thresholds from a captured Luck value and worst-to-best base bands.</summary>
    /// <param name="group">One uniform die, with all band thresholds within its natural range.</param>
    /// <param name="luck">Captured nonnegative generation context.</param>
    /// <returns>An immutable single-roll group, omitting outcomes with zero representable probability.</returns>
    /// <exception cref="ArgumentNullException">An input is null.</exception>
    /// <exception cref="ArgumentException">The base is not a uniform fully covered distribution or resolution is incompatible.</exception>
    public LootGroupDefinition Apply(LootGroupDefinition group, GenerationLuck luck)
    {
        ArgumentNullException.ThrowIfNull(group);
        ArgumentNullException.ThrowIfNull(luck);

        if (group.Dice.Count != 1 || group.Bands[0].MinimumRoll != 1
            || group.Bands.Any(band => band.MinimumRoll > group.Dice.Sides)
            || Resolution % group.Dice.Sides != 0)
        {
            throw new ArgumentException("Luck needs ordered outcomes on one uniform die and an exact resolution multiple.", nameof(group));
        }

        var exponent = 1d + (double)luck.Value / (double)Scale;
        var bands = new List<LootBand>();
        var lower = 0;

        for (var index = 0; index < group.Bands.Count; index++)
        {
            var upperBase = index + 1 < group.Bands.Count ? group.Bands[index + 1].MinimumRoll - 1 : group.Dice.Sides;
            var upper = luck.Value == 0 ? upperBase * (Resolution / group.Dice.Sides)
                : upperBase == group.Dice.Sides ? Resolution
                : (int)Math.Round(Resolution * Math.Pow((double)upperBase / group.Dice.Sides, exponent), MidpointRounding.AwayFromZero);

            if (upper > lower)
            {
                bands.Add(new(lower + 1, group.Bands[index].Outcome));
            }

            lower = upper;
        }

        return new(group.Id, new(1, Resolution), bands);
    }
}
