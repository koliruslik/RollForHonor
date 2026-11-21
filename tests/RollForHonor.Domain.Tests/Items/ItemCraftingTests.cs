using RollForHonor.Domain.Items.Configuration;
using RollForHonor.Domain.Items.Models;
using RollForHonor.Domain.Items.Services;
using RollForHonor.Domain.Stats;

namespace RollForHonor.Domain.Tests.Items;

/// <summary>Verifies explicit rarity, bounded crafting, compatibility and immutable owned values.</summary>
public sealed class ItemCraftingTests
{
    /// <summary>Initial capacities are a separate rule table.</summary>
    /// <param name="rarity">The explicit rarity category.</param>
    /// <param name="capacity">The approved initial affix capacity.</param>
    [Theory]
    [InlineData(ItemRarity.Common, 0)]
    [InlineData(ItemRarity.Uncommon, 2)]
    [InlineData(ItemRarity.Rare, 4)]
    [InlineData(ItemRarity.Epic, 6)]
    [InlineData(ItemRarity.Legendary, 8)]
    public void RarityRulesUseApprovedInitialCapacities(ItemRarity rarity, int capacity)
    {
        Assert.Equal(capacity, ItemRarityRules.Default.GetCapacity(rarity));
    }

    /// <summary>Replacing the capacity table changes validation without changing item code or global defaults.</summary>
    [Fact]
    public void InjectedCapacityRulesOwnTheirInput()
    {
        var entries = Enum.GetValues<ItemRarity>()
            .Select(rarity => new KeyValuePair<ItemRarity, int>(rarity, 1))
            .ToArray();
        var rules = new ItemRarityRules(entries);
        entries[0] = new(ItemRarity.Common, 99);
        var factory = ItemTestContent.CreateFactory(rules);
        var affix = ItemTestContent.Roll(ItemTestContent.CreateAffix("health"), "owned");

        var item = factory.Create("item", new ItemDefinition("base", "Base"), ItemRarity.Common, [affix]);

        Assert.Single(item.Affixes);
        Assert.Equal(1, rules.GetCapacity(ItemRarity.Common));
        Assert.Equal(0, ItemRarityRules.Default.GetCapacity(ItemRarity.Common));
        Assert.Throws<ArgumentException>(() => new ItemRarityRules(entries.Skip(1)));
    }

    /// <summary>Full items reject additions until a separate explicit rarity upgrade takes place.</summary>
    [Fact]
    public void FillingSlotsNeverUpgradesRarityAndExplicitUpgradePreservesRolls()
    {
        var factory = ItemTestContent.CreateFactory();
        var crafting = new ItemCraftingService(factory);
        var definition = new ItemDefinition("base", "Base");
        var first = ItemTestContent.Roll(ItemTestContent.CreateAffix("one"), "one");
        var second = ItemTestContent.Roll(ItemTestContent.CreateAffix("two"), "two");
        var third = ItemTestContent.Roll(ItemTestContent.CreateAffix("three"), "three");
        var original = factory.Create("item", definition, ItemRarity.Uncommon, [first, second]);

        Assert.Throws<ArgumentException>(() => crafting.AddAffix(original, third));
        var upgraded = crafting.UpgradeRarity(original, ItemRarity.Rare);
        var filled = crafting.AddAffix(upgraded, third);
        var removed = crafting.RemoveAffix(filled, third.Id);

        Assert.Equal(ItemRarity.Uncommon, original.Rarity);
        Assert.Equal(2, original.Affixes.Count);
        Assert.Equal(ItemRarity.Rare, removed.Rarity);
        Assert.Same(first, upgraded.Affixes[0]);
        Assert.Equal(3, filled.Affixes.Count);
        Assert.Throws<ArgumentException>(() => crafting.UpgradeRarity(upgraded, ItemRarity.Rare));
        Assert.Throws<ArgumentException>(() => crafting.UpgradeRarity(upgraded, ItemRarity.Common));
    }

    /// <summary>An empty Legendary item retains its rarity, while implicit modifiers do not consume slots.</summary>
    [Fact]
    public void ExplicitRarityIsIndependentOfAffixCountAndImplicitModifiers()
    {
        var definition = new ItemDefinition("base", "Base", modifiers:
            [new ItemModifier(new StatKey(StatType.Armor), StatOperation.Flat, 5)]);
        var factory = ItemTestContent.CreateFactory();

        var legendary = factory.Create("legendary", definition, ItemRarity.Legendary);
        var common = factory.Create("common", definition, ItemRarity.Common);

        Assert.Equal(ItemRarity.Legendary, legendary.Rarity);
        Assert.Empty(legendary.Affixes);
        Assert.Single(common.Base.Modifiers);
    }

    /// <summary>Item validation rejects incompatible tags, duplicate owned IDs and conflicting affix families.</summary>
    [Fact]
    public void CompatibilityAndExclusivityApplyToCreationAndCrafting()
    {
        var factory = ItemTestContent.CreateFactory();
        var crafting = new ItemCraftingService(factory);
        var definition = new ItemDefinition("base", "Base", ["armor"]);
        var first = ItemTestContent.Roll(ItemTestContent.CreateAffix("one", family: "health"), "one");
        var sameFamily = ItemTestContent.Roll(ItemTestContent.CreateAffix("two", family: "health"), "two");
        var sameId = ItemTestContent.Roll(ItemTestContent.CreateAffix("three"), "one");
        var weaponOnly = ItemTestContent.Roll(ItemTestContent.CreateAffix("four", tags: ["weapon"]), "four");
        var original = factory.Create("item", definition, ItemRarity.Rare, [first]);

        Assert.Throws<ArgumentException>(() => crafting.AddAffix(original, sameFamily));
        Assert.Throws<ArgumentException>(() => crafting.AddAffix(original, sameId));
        Assert.Throws<ArgumentException>(() => crafting.AddAffix(original, weaponOnly));
        Assert.Throws<ArgumentException>(() => crafting.ReplaceAffix(original, first.Id, weaponOnly));
        Assert.Throws<KeyNotFoundException>(() => crafting.RemoveAffix(original, "missing"));
        Assert.Throws<KeyNotFoundException>(() => crafting.ReplaceAffix(original, "missing", sameFamily));
        Assert.Same(first, Assert.Single(original.Affixes));
    }

    /// <summary>Value replacement preserves occupied count, rarity, base and unaffected affixes.</summary>
    [Fact]
    public void ReplacingAnAffixKeepsTheOtherOwnedValues()
    {
        var factory = ItemTestContent.CreateFactory();
        var crafting = new ItemCraftingService(factory);
        var definition = ItemTestContent.CreateAffix("health");
        var first = ItemTestContent.Roll(definition, "owned", 1);
        var rerolled = ItemTestContent.Roll(definition, "owned", 100);
        var other = ItemTestContent.Roll(ItemTestContent.CreateAffix("mana"), "other", 50);
        var item = factory.Create("item", new ItemDefinition("base", "Base"), ItemRarity.Rare, [first, other]);

        var changed = crafting.ReplaceAffix(item, first.Id, rerolled);

        Assert.Equal(10, Assert.Single(item.Affixes[0].Modifiers).Amount);
        Assert.Equal(20, Assert.Single(changed.Affixes[0].Modifiers).Amount);
        Assert.Same(item.Base, changed.Base);
        Assert.Equal(item.Rarity, changed.Rarity);
        Assert.Equal(2, changed.Affixes.Count);
        Assert.Same(other, changed.Affixes[1]);
        Assert.Equal(other.Modifiers, changed.Affixes[1].Modifiers);
    }

    /// <summary>An explicit upgrade cannot discard occupied affixes when a custom target capacity is smaller.</summary>
    [Fact]
    public void UpgradeWithInsufficientConfiguredCapacityLeavesTheOriginalUnchanged()
    {
        var capacities = Enum.GetValues<ItemRarity>()
            .Select(rarity => new KeyValuePair<ItemRarity, int>(rarity, rarity == ItemRarity.Legendary ? 1 : 4));
        var factory = ItemTestContent.CreateFactory(new ItemRarityRules(capacities));
        var crafting = new ItemCraftingService(factory);
        var first = ItemTestContent.Roll(ItemTestContent.CreateAffix("health"), "one");
        var second = ItemTestContent.Roll(ItemTestContent.CreateAffix("mana"), "two");
        var item = factory.Create("item", new ItemDefinition("base", "Base"), ItemRarity.Epic, [first, second]);

        Assert.Throws<ArgumentException>(() => crafting.UpgradeRarity(item, ItemRarity.Legendary));

        Assert.Equal(ItemRarity.Epic, item.Rarity);
        Assert.Equal(2, item.Affixes.Count);
        Assert.Same(first, item.Affixes[0]);
        Assert.Same(second, item.Affixes[1]);
    }
}
