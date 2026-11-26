using RollForHonor.Domain.Dice.Models;

namespace RollForHonor.Domain.Rewards;

/// <summary>Defines an independent loot-group roll with ordered, gap-free outcome bands.</summary>
public sealed class LootGroupDefinition
{
    /// <summary>Gets the stable group identity, such as gold, equipment, weapons or jewelry.</summary>
    public string Id { get; }

    /// <summary>Gets the configured group-selection dice, independent of the gold-quantity d100.</summary>
    public DiceFormula Dice { get; }

    /// <summary>Gets immutable thresholds in ascending order; the last band has no upper cap.</summary>
    public IReadOnlyList<LootBand> Bands { get; }

    /// <summary>Defines one group without assigning gameplay probabilities or a Luck formula.</summary>
    /// <param name="id">The nonblank group identity.</param>
    /// <param name="dice">The nonnull selection formula.</param>
    /// <param name="bands">Nonempty distinct thresholds; the first must cover the minimum natural roll.</param>
    /// <exception cref="ArgumentNullException">The dice or bands are null.</exception>
    /// <exception cref="ArgumentException">The identity is blank, bands are empty/null/duplicated, or the lowest roll has no outcome.</exception>
    public LootGroupDefinition(string id, DiceFormula dice, IEnumerable<LootBand> bands)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        ArgumentNullException.ThrowIfNull(dice);
        ArgumentNullException.ThrowIfNull(bands);
        var entries = bands.ToArray();

        if (entries.Length == 0 || entries.Any(band => band is null)
            || entries
                .Select(band => band.MinimumRoll)
                .Distinct()
                .Count() != entries.Length)
        {
            throw new ArgumentException("Loot groups require nonnull distinct outcome thresholds.", nameof(bands));
        }

        entries = entries
            .OrderBy(band => band.MinimumRoll)
            .ToArray();

        if (entries[0].MinimumRoll > dice.Count)
        {
            throw new ArgumentException("The first band must cover the minimum natural roll.", nameof(bands));
        }

        Id = id;
        Dice = dice;
        Bands = Array.AsReadOnly(entries);
    }

    /// <summary>Selects the highest threshold reached by a modified result.</summary>
    /// <param name="modifiedRoll">The natural group total plus a nonnegative Luck bonus.</param>
    /// <returns>Exactly one outcome, which may explicitly be empty.</returns>
    /// <exception cref="ArgumentOutOfRangeException">The result is below the minimum natural roll.</exception>
    public LootOutcome Select(int modifiedRoll)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(modifiedRoll, Dice.Count);

        return Bands.Last(band => band.MinimumRoll <= modifiedRoll).Outcome;
    }
}
