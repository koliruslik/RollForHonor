using RollForHonor.Domain.Items.Affixes.Models;
using RollForHonor.Domain.Items.Models;

namespace RollForHonor.Domain.Items.Services;

/// <summary>Changes immutable item values within capacity; rarity upgrades remain explicit and cost-free at this domain boundary.</summary>
public sealed class ItemCraftingService
{
    private readonly ItemFactory _factory;

    /// <summary>Shares the same validation and capacity rules as item creation.</summary>
    /// <param name="factory">The authoritative item factory.</param>
    /// <exception cref="ArgumentNullException">The factory is null.</exception>
    public ItemCraftingService(ItemFactory factory)
    {
        ArgumentNullException.ThrowIfNull(factory);

        _factory = factory;
    }

    /// <summary>Adds an already rolled affix without changing rarity.</summary>
    /// <param name="item">The immutable original item.</param>
    /// <param name="affix">The affix to add.</param>
    /// <returns>A new item with the additional affix.</returns>
    /// <exception cref="ArgumentNullException">An input is null.</exception>
    /// <exception cref="ArgumentException">Capacity, compatibility, identity, family or stat-operation rules are violated.</exception>
    public ItemInstance AddAffix(ItemInstance item, AffixInstance affix)
    {
        ArgumentNullException.ThrowIfNull(item);
        ArgumentNullException.ThrowIfNull(affix);

        return _factory.Create(item.Id, item.Base, item.Rarity, item.Affixes.Append(affix));
    }

    /// <summary>Replaces exactly one affix, also supporting a value reroll supplied by the affix roller.</summary>
    /// <param name="item">The immutable original item.</param>
    /// <param name="affixId">The owned affix identity to replace.</param>
    /// <param name="replacement">The new rolled affix.</param>
    /// <returns>A new item retaining all unaffected values and its rarity.</returns>
    /// <exception cref="ArgumentNullException">The item or replacement is null.</exception>
    /// <exception cref="ArgumentException">The ID is blank or the resulting item violates its rules.</exception>
    /// <exception cref="KeyNotFoundException">The item has no affix with that identity.</exception>
    public ItemInstance ReplaceAffix(ItemInstance item, string affixId, AffixInstance replacement)
    {
        ArgumentNullException.ThrowIfNull(item);
        ArgumentNullException.ThrowIfNull(replacement);
        RequireAffix(item, affixId);

        var affixes = item.Affixes
            .Select(affix => affix.Id == affixId ? replacement : affix);

        return _factory.Create(item.Id, item.Base, item.Rarity, affixes);
    }

    /// <summary>Removes an affix without lowering rarity or changing the other rolls.</summary>
    /// <param name="item">The immutable original item.</param>
    /// <param name="affixId">The owned affix identity to remove.</param>
    /// <returns>A new item with one free slot.</returns>
    /// <exception cref="ArgumentNullException">The item is null.</exception>
    /// <exception cref="ArgumentException">The ID is blank or the remaining item violates the current rules.</exception>
    /// <exception cref="KeyNotFoundException">The item has no affix with that identity.</exception>
    public ItemInstance RemoveAffix(ItemInstance item, string affixId)
    {
        ArgumentNullException.ThrowIfNull(item);
        RequireAffix(item, affixId);

        var affixes = item.Affixes
            .Where(affix => affix.Id != affixId);

        return _factory.Create(item.Id, item.Base, item.Rarity, affixes);
    }

    /// <summary>Explicitly raises rarity while preserving occupied slots; callers must enforce any future resource/recipe requirements.</summary>
    /// <param name="item">The immutable original item.</param>
    /// <param name="rarity">A defined rarity strictly higher than the original.</param>
    /// <returns>A new item with the requested rarity and unchanged rolls.</returns>
    /// <exception cref="ArgumentNullException">The item is null.</exception>
    /// <exception cref="ArgumentException">Rarity does not increase or the new capacity cannot hold the affixes.</exception>
    /// <exception cref="ArgumentOutOfRangeException">The target rarity is undefined.</exception>
    public ItemInstance UpgradeRarity(ItemInstance item, ItemRarity rarity)
    {
        ArgumentNullException.ThrowIfNull(item);

        if (!Enum.IsDefined(rarity))
        {
            throw new ArgumentOutOfRangeException(nameof(rarity));
        }

        if (rarity <= item.Rarity)
        {
            throw new ArgumentException("Rarity upgrades must explicitly increase rarity.", nameof(rarity));
        }

        return _factory.Create(item.Id, item.Base, rarity, item.Affixes);
    }

    private static void RequireAffix(ItemInstance item, string affixId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(affixId);

        if (!item.Affixes.Any(affix => affix.Id == affixId))
        {
            throw new KeyNotFoundException("The item does not own the requested affix.");
        }
    }
}
