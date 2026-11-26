using RollForHonor.Domain.Items.Affixes.Models;
using RollForHonor.Domain.Items.Configuration;
using RollForHonor.Domain.Items.Models;

namespace RollForHonor.Domain.Items.Services;

/// <summary>Creates valid immutable items using injected capacity and stat-operation rules.</summary>
public sealed class ItemFactory
{
    /// <summary>Gets the capacity table used for creation and subsequent crafting.</summary>
    public ItemRarityRules RarityRules { get; }

    private readonly ItemStatRules _statRules;

    /// <summary>Uses explicit rules rather than embedding balance constants into item instances.</summary>
    /// <param name="rarityRules">The capacity rules.</param>
    /// <param name="statRules">The operations allowed in item content.</param>
    /// <exception cref="ArgumentNullException">A rule set is null.</exception>
    public ItemFactory(ItemRarityRules rarityRules, ItemStatRules statRules)
    {
        ArgumentNullException.ThrowIfNull(rarityRules);
        ArgumentNullException.ThrowIfNull(statRules);

        RarityRules = rarityRules;
        _statRules = statRules;
    }

    /// <summary>Validates a base and occupied affixes, preserving the requested rarity even when slots are empty.</summary>
    /// <param name="id">The nonblank identity allocated by the caller.</param>
    /// <param name="definition">The base snapshot to retain.</param>
    /// <param name="rarity">The explicit rarity.</param>
    /// <param name="affixes">Optional existing affixes, copied in supplied order.</param>
    /// <param name="generationLevel">The positive immutable affix-gating level.</param>
    /// <returns>A new immutable item with no random rolls.</returns>
    /// <exception cref="ArgumentNullException">The definition is null.</exception>
    /// <exception cref="ArgumentException">Identity, capacity, compatibility, affix uniqueness or stat rules are violated.</exception>
    /// <exception cref="ArgumentOutOfRangeException">The rarity is undefined or the generation level is not positive.</exception>
    public ItemInstance Create(string id, ItemDefinition definition, ItemRarity rarity, IEnumerable<AffixInstance>? affixes = null, int generationLevel = 1)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(generationLevel);
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        ArgumentNullException.ThrowIfNull(definition);

        var entries = affixes?.ToArray() ?? [];
        ValidateAffixes(definition, rarity, entries);
        ValidateModifiers(definition, entries);

        return new ItemInstance(id, definition, rarity, entries, generationLevel);
    }

    private void ValidateAffixes(ItemDefinition definition, ItemRarity rarity, AffixInstance[] affixes)
    {
        if (affixes.Length > RarityRules.GetCapacity(rarity) || affixes.Any(affix => affix is null))
        {
            throw new ArgumentException("Affixes must be nonnull and fit the rarity's capacity.", nameof(affixes));
        }

        ValidateAffixIdentity(affixes);

        if (affixes.Any(affix => !affix.RequiredItemTags.IsSubsetOf(definition.Tags)))
        {
            throw new ArgumentException("An affix is incompatible with the base's tags.", nameof(affixes));
        }
    }

    private void ValidateModifiers(ItemDefinition definition, AffixInstance[] affixes)
    {
        var modifiers = definition.Modifiers
            .Concat(affixes.SelectMany(affix => affix.Modifiers));

        foreach (var modifier in modifiers)
        {
            _statRules.Validate(modifier);

            if (modifier.Key.Type == RollForHonor.Domain.Stats.StatType.CriticalRollBonus
                && (!definition.Tags.Contains("amulet") && !definition.Tags.Contains("ring") || modifier.Amount < 0 || modifier.Amount > 3))
            {
                throw new ArgumentException("Critical roll bonuses are limited to 0 through +3 on amulets and rings.", nameof(definition));
            }
        }
    }

    private static void ValidateAffixIdentity(AffixInstance[] affixes)
    {
        var identityCount = affixes
            .DistinctBy(affix => affix.Id)
            .Count();
        var familyCount = affixes
            .DistinctBy(affix => affix.FamilyId)
            .Count();

        if (identityCount != affixes.Length || familyCount != affixes.Length)
        {
            throw new ArgumentException("Affix identities and exclusivity families must be unique.", nameof(affixes));
        }
    }
}
