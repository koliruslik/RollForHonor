using System.Collections.Immutable;
using RollForHonor.Domain.Items.Models;

namespace RollForHonor.Domain.Items.Configuration;

/// <summary>Owns replaceable affix capacities; rarity never follows the current affix count automatically.</summary>
public sealed class ItemRarityRules
{
    /// <summary>Gets the initial Common through Legendary capacities: 0, 2, 4, 6 and 8.</summary>
    public static ItemRarityRules Default { get; } = new(
    [
        new(ItemRarity.Common, 0),
        new(ItemRarity.Uncommon, 2),
        new(ItemRarity.Rare, 4),
        new(ItemRarity.Epic, 6),
        new(ItemRarity.Legendary, 8)
    ]);

    private readonly ImmutableDictionary<ItemRarity, int> _capacities;

    /// <summary>Copies a complete capacity table for all defined rarities.</summary>
    /// <param name="capacities">Unique, defined rarities with nonnegative affix capacities.</param>
    /// <exception cref="ArgumentNullException">The table is null.</exception>
    /// <exception cref="ArgumentException">Entries are missing, duplicated, undefined or negative.</exception>
    public ItemRarityRules(IEnumerable<KeyValuePair<ItemRarity, int>> capacities)
    {
        ArgumentNullException.ThrowIfNull(capacities);

        _capacities = capacities.ToImmutableDictionary();

        if (_capacities.Count != Enum.GetValues<ItemRarity>().Length
            || _capacities.Any(pair => !Enum.IsDefined(pair.Key) || pair.Value < 0))
        {
            throw new ArgumentException("Define a nonnegative capacity for every rarity.", nameof(capacities));
        }
    }

    /// <summary>Reads the maximum number of affixes allowed by a rarity.</summary>
    /// <param name="rarity">A defined item rarity.</param>
    /// <returns>The configured capacity, independent of occupied slots.</returns>
    /// <exception cref="ArgumentOutOfRangeException">The rarity is undefined.</exception>
    public int GetCapacity(ItemRarity rarity)
    {
        if (!Enum.IsDefined(rarity))
        {
            throw new ArgumentOutOfRangeException(nameof(rarity));
        }

        return _capacities[rarity];
    }
}
