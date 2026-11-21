using System.Collections.Immutable;
using RollForHonor.Domain.Items.Affixes.Models;
using RollForHonor.Domain.Items.Models;

namespace RollForHonor.Domain.Items.Configuration;

/// <summary>Owns a validated immutable content pool, separate from the state of generated items.</summary>
public sealed class ItemCatalog
{
    /// <summary>Gets affix definitions in stable identifier order for reproducible selection.</summary>
    public IReadOnlyList<AffixDefinition> Affixes { get; }

    private readonly ImmutableDictionary<string, ItemDefinition> _items;

    /// <summary>Copies unique definitions and validates all their modifiers against item stat rules.</summary>
    /// <param name="items">Non-null bases with unique ordinal IDs.</param>
    /// <param name="affixes">Non-null affix definitions with unique ordinal IDs.</param>
    /// <param name="statRules">The allowed item-modifier operations.</param>
    /// <exception cref="ArgumentNullException">An input is null.</exception>
    /// <exception cref="ArgumentException">An entry is null, IDs repeat or a modifier operation is forbidden.</exception>
    public ItemCatalog(IEnumerable<ItemDefinition> items, IEnumerable<AffixDefinition> affixes, ItemStatRules statRules)
    {
        ArgumentNullException.ThrowIfNull(items);
        ArgumentNullException.ThrowIfNull(affixes);
        ArgumentNullException.ThrowIfNull(statRules);

        var bases = items.ToArray();
        var options = affixes.ToArray();
        ValidateEntries(bases, options);
        ValidateModifiers(bases, options, statRules);

        _items = bases.ToImmutableDictionary(item => item.Id, StringComparer.Ordinal);
        Affixes = Array.AsReadOnly(options
            .OrderBy(affix => affix.Id, StringComparer.Ordinal)
            .ToArray());
    }

    /// <summary>Finds an immutable base by its stable content identifier.</summary>
    /// <param name="id">The base identifier.</param>
    /// <returns>The matching base definition.</returns>
    /// <exception cref="ArgumentException">The ID is blank.</exception>
    /// <exception cref="KeyNotFoundException">The catalog contains no matching base.</exception>
    public ItemDefinition GetItem(string id)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);

        return _items[id];
    }

    private static void ValidateEntries(ItemDefinition[] items, AffixDefinition[] affixes)
    {
        if (items.Any(item => item is null) || affixes.Any(affix => affix is null))
        {
            throw new ArgumentException("Content collections cannot contain null.");
        }

        var itemCount = items
            .DistinctBy(item => item.Id)
            .Count();
        var affixCount = affixes
            .DistinctBy(affix => affix.Id)
            .Count();

        if (itemCount != items.Length || affixCount != affixes.Length)
        {
            throw new ArgumentException("Content identifiers must be unique within their category.");
        }
    }

    private static void ValidateModifiers(ItemDefinition[] items, AffixDefinition[] affixes, ItemStatRules rules)
    {
        var implicitModifiers = items.SelectMany(item => item.Modifiers);
        var rangeEndpoints = affixes
            .SelectMany(affix => affix.Tiers
                .SelectMany(tier => tier.Modifiers
                    .SelectMany(range => new[]
                    {
                        new ItemModifier(range.Key, range.Operation, range.Minimum),
                        new ItemModifier(range.Key, range.Operation, range.Maximum)
                    })));

        foreach (var modifier in implicitModifiers.Concat(rangeEndpoints))
        {
            rules.Validate(modifier);
        }
    }
}
