using RollForHonor.Domain.Combat.Damage.Models;
using RollForHonor.Domain.Items.Affixes.Models;
using RollForHonor.Domain.Items.Affixes.Services;
using RollForHonor.Domain.Items.Configuration;
using RollForHonor.Domain.Items.Models;
using RollForHonor.Domain.Items.Services;
using RollForHonor.Domain.Stats;

namespace RollForHonor.Domain.Tests.Items;

/// <summary>Verifies item-operation rules, stored value ownership and integration with the shared stat calculator.</summary>
public sealed class ItemModifierTests
{
    /// <summary>Item content follows the approved operation categories.</summary>
    /// <param name="type">The stat category.</param>
    /// <param name="operation">The stacking operation.</param>
    /// <param name="allowed">Whether the operation is permitted on items.</param>
    [Theory]
    [InlineData(StatType.AttackModifier, StatOperation.Flat, true)]
    [InlineData(StatType.AttackModifier, StatOperation.Increased, false)]
    [InlineData(StatType.AttackModifier, StatOperation.More, false)]
    [InlineData(StatType.MovementSpeed, StatOperation.Flat, false)]
    [InlineData(StatType.MovementSpeed, StatOperation.Increased, true)]
    [InlineData(StatType.CriticalChance, StatOperation.Flat, false)]
    [InlineData(StatType.CriticalMultiplier, StatOperation.Flat, false)]
    [InlineData(StatType.DamageMultiplier, StatOperation.Flat, false)]
    [InlineData(StatType.DamageMultiplier, StatOperation.More, true)]
    [InlineData(StatType.MaxHealth, StatOperation.Flat, true)]
    [InlineData(StatType.MaxHealth, StatOperation.Increased, true)]
    [InlineData(StatType.Resistance, StatOperation.Flat, true)]
    [InlineData(StatType.Resistance, StatOperation.More, true)]
    public void DefaultItemOperationsMatchStatCategories(StatType type, StatOperation operation, bool allowed)
    {
        Assert.Equal(allowed, ItemStatRules.Default.Allows(type, operation));
    }

    /// <summary>Catalog and standalone item creation both enforce item-specific operation rules.</summary>
    [Fact]
    public void InvalidItemOperationsAreRejectedBeforeGeneration()
    {
        var invalid = new ItemDefinition("base", "Base", modifiers:
            [new ItemModifier(new StatKey(StatType.AttackModifier), StatOperation.Increased, 0.1m)]);
        var invalidAffix = new AffixDefinition("speed", "speed", 1,
        [
            new AffixTier("first", 0,
            [
                new AffixModifierRange(new StatKey(StatType.MovementSpeed), StatOperation.Flat, 1, 2)
            ])
        ]);

        Assert.Throws<ArgumentException>(() => new ItemCatalog([invalid], [], ItemStatRules.Default));
        Assert.Throws<ArgumentException>(() => new ItemCatalog([], [invalidAffix], ItemStatRules.Default));
        Assert.Throws<ArgumentException>(() => ItemTestContent.CreateFactory().Create("item", invalid, ItemRarity.Common));
    }

    /// <summary>One affix can grant several modifiers, including a resistance-to-all bundle without a new combat stat.</summary>
    [Fact]
    public void MultiStatAffixesProjectIntoExistingCalculatorWithStableOrigins()
    {
        var health = new StatKey(StatType.MaxHealth);
        var ranges = Enum.GetValues<DamageType>()
            .Select(type => new AffixModifierRange(new StatKey(StatType.Resistance, type), StatOperation.Flat, 0.05m, 0.1m, 2))
            .Concat(
            [
                new AffixModifierRange(health, StatOperation.Flat, 20, 20),
                new AffixModifierRange(health, StatOperation.Increased, 0.2m, 0.2m, 2)
            ])
            .ToArray();
        var definition = new AffixDefinition("bundle", "bundle", 1, [new AffixTier("first", 0, ranges)]);
        var dice = new SequenceDiceRoller(100);
        var rolled = new AffixRoller().Roll("owned", definition, "first", dice);
        var item = ItemTestContent.CreateFactory().Create("item", new ItemDefinition("base", "Base"), ItemRarity.Uncommon, [rolled]);
        var projector = new ItemModifierProjector();

        var modifiers = projector.Project(item);
        var stats = new StatCalculator().Calculate(new StatProfile(new StatBlock([new(health, 100)]), modifiers));

        Assert.Equal(144, stats.Get(health));
        Assert.All(Enum.GetValues<DamageType>(), type => Assert.Equal(0.1m, stats.Get(new StatKey(StatType.Resistance, type))));
        var sources = modifiers
            .Select(modifier => modifier.Source)
            .Distinct();

        Assert.Single(sources);
        Assert.Single(dice.Trace.Rolls);
        Assert.Equal(modifiers, projector.Project(item));
    }

    /// <summary>Implicit and rolled sources remain distinct even when user-provided IDs contain delimiters.</summary>
    [Fact]
    public void OriginEncodingDoesNotCollideAcrossItemAndAffixIdentifiers()
    {
        var factory = ItemTestContent.CreateFactory();
        var affix = ItemTestContent.Roll(ItemTestContent.CreateAffix("health"), "a:Affix:1:b");
        var definition = new ItemDefinition("base", "Base", modifiers:
            [new ItemModifier(new StatKey(StatType.Armor), StatOperation.Flat, 5)]);
        var first = factory.Create("a", definition, ItemRarity.Rare, [affix]);
        var second = factory.Create("a:Affix:1:b", definition, ItemRarity.Rare,
            [ItemTestContent.Roll(ItemTestContent.CreateAffix("health"), "a")]);
        var projector = new ItemModifierProjector();

        var sources = projector.Project(first)
            .Concat(projector.Project(second))
            .Select(modifier => modifier.Source)
            .ToArray();

        var sourceCount = sources
            .Distinct()
            .Count();

        Assert.Equal(4, sourceCount);
    }

    /// <summary>Content inputs and owned affix lists are copied before use and exposed without mutation.</summary>
    [Fact]
    public void DefinitionsAndOwnedInstancesDoNotRetainMutableInputCollections()
    {
        var tags = new List<string> { "armor" };
        var implicitValues = new List<ItemModifier> { new(new StatKey(StatType.Armor), StatOperation.Flat, 5) };
        var definition = new ItemDefinition("base", "Base", tags, implicitValues);
        var ranges = new List<AffixModifierRange> { new(new StatKey(StatType.MaxHealth), StatOperation.Flat, 10, 20) };
        var tiers = new List<AffixTier> { new("first", 0, ranges) };
        var affixDefinition = new AffixDefinition("health", "health", 1, tiers, tags);
        var affix = ItemTestContent.Roll(affixDefinition, "owned");
        var owned = new List<AffixInstance> { affix };
        var item = ItemTestContent.CreateFactory().Create("item", definition, ItemRarity.Rare, owned);

        tags.Clear();
        implicitValues.Clear();
        ranges.Clear();
        tiers.Clear();
        owned.Clear();

        Assert.Contains("armor", item.Base.Tags);
        Assert.Single(item.Base.Modifiers);
        Assert.Single(item.Affixes);
        Assert.Single(affixDefinition.Tiers);
        Assert.Single(affixDefinition.Tiers[0].Modifiers);
        Assert.Throws<NotSupportedException>(() => ((IList<AffixInstance>)item.Affixes).Clear());
    }

    /// <summary>Replacing catalog content with the same IDs affects new rolls but never rewrites an owned item's stored values.</summary>
    [Fact]
    public void RebalancedCatalogDoesNotChangeExistingItems()
    {
        var originalBase = new ItemDefinition("base", "Base", modifiers:
            [new ItemModifier(new StatKey(StatType.Armor), StatOperation.Flat, 5)]);
        var originalGenerator = ItemTestContent.CreateGenerator([ItemTestContent.CreateAffix("health")], item: originalBase);
        var request = new ItemGenerationRequest("old-item", "base", ItemRarity.Uncommon, 1, 0);
        var original = originalGenerator.Generate(request, new SequenceDiceRoller(100));
        var projector = new ItemModifierProjector();
        var before = projector.Project(original);
        var rebalancedBase = new ItemDefinition("base", "Base", modifiers:
            [new ItemModifier(new StatKey(StatType.Armor), StatOperation.Flat, 50)]);
        var rebalancedAffix = new AffixDefinition("health", "health", 1,
        [
            new AffixTier("first", 0,
            [
                new AffixModifierRange(new StatKey(StatType.MaxHealth), StatOperation.Flat, 100, 200)
            ])
        ]);
        var rebalancedGenerator = ItemTestContent.CreateGenerator([rebalancedAffix], item: rebalancedBase);

        var fresh = rebalancedGenerator.Generate(
            new ItemGenerationRequest("new-item", "base", ItemRarity.Uncommon, 1, 0), new SequenceDiceRoller(100));

        Assert.Equal(before, projector.Project(original));
        Assert.Equal(5, Assert.Single(original.Base.Modifiers).Amount);
        Assert.Equal(20, Assert.Single(original.Affixes[0].Modifiers).Amount);
        Assert.Equal(50, Assert.Single(fresh.Base.Modifiers).Amount);
        Assert.Equal(200, Assert.Single(fresh.Affixes[0].Modifiers).Amount);
    }

    /// <summary>Invalid IDs, ranges and conflicting definitions fail explicitly.</summary>
    [Fact]
    public void ContentValidationRejectsAmbiguousDefinitions()
    {
        var key = new StatKey(StatType.MaxHealth);
        var definition = ItemTestContent.CreateAffix("health");

        Assert.Throws<ArgumentException>(() => new ItemDefinition("", "Base"));
        Assert.Throws<ArgumentException>(() => new AffixModifierRange(key, StatOperation.Flat, 20, 10));
        Assert.Throws<ArgumentException>(() => new AffixModifierRange(key, StatOperation.Flat, 0.1m, 1, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => new AffixModifierRange(key, StatOperation.More, -2, 0));
        Assert.Throws<ArgumentException>(() => new AffixDefinition("a", "a", 1, []));
        Assert.Throws<ArgumentException>(() => new AffixDefinition("a", "a", 1, [definition.Tiers[0], definition.Tiers[0]]));
        Assert.Throws<ArgumentException>(() => new ItemCatalog([], [definition, definition], ItemStatRules.Default));
        Assert.Throws<ArgumentOutOfRangeException>(() => new ItemGenerationRequest("item", "base", ItemRarity.Rare, -1, 0));
    }
}
