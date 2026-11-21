using System.Globalization;
using RollForHonor.Domain.Items.Affixes.Models;
using RollForHonor.Domain.Items.Affixes.Services;
using RollForHonor.Domain.Items.Configuration;
using RollForHonor.Domain.Items.Models;
using RollForHonor.Domain.Stats;

namespace RollForHonor.Domain.Tests.Items;

/// <summary>Verifies deterministic content selection, explicit tier policies and bounded pool failure.</summary>
public sealed class ItemGenerationTests
{
    /// <summary>The same seed and content reproduce results regardless of catalog insertion order.</summary>
    [Fact]
    public void SeedAndCanonicalContentOrderReproduceOwnedValues()
    {
        var affixes = Enumerable.Range(0, 6)
            .Select(index => ItemTestContent.CreateAffix($"affix-{index}", weight: index + 1))
            .ToArray();
        var request = new ItemGenerationRequest("owned", "base", ItemRarity.Rare, 4, 20);

        var first = ItemTestContent.CreateGenerator(affixes).Generate(request, new SeededItemDiceRoller(42));
        var second = ItemTestContent.CreateGenerator(affixes.Reverse()).Generate(request, new SeededItemDiceRoller(42));

        Assert.Equal(first.Affixes.Select(affix => affix.DefinitionId), second.Affixes.Select(affix => affix.DefinitionId));
        Assert.Equal(first.Affixes.Select(affix => affix.QualityRoll), second.Affixes.Select(affix => affix.QualityRoll));
        Assert.Equal(first.Affixes.SelectMany(affix => affix.Modifiers), second.Affixes.SelectMany(affix => affix.Modifiers));
        var familyCount = first.Affixes
            .DistinctBy(affix => affix.FamilyId)
            .Count();

        Assert.Equal(4, familyCount);
    }

    /// <summary>Eligibility is checked before randomness, avoiding retry loops and partial results.</summary>
    [Fact]
    public void InsufficientCompatibleFamiliesFailBeforeConsumingDice()
    {
        var generator = ItemTestContent.CreateGenerator(
        [
            ItemTestContent.CreateAffix("health-a", family: "health"),
            ItemTestContent.CreateAffix("health-b", family: "health"),
            ItemTestContent.CreateAffix("weapon", tags: ["weapon"]),
            ItemTestContent.CreateAffix("expensive", power: 10)
        ]);
        var dice = new SequenceDiceRoller();

        Assert.Throws<InvalidOperationException>(() => generator.Generate(
            new ItemGenerationRequest("item", "base", ItemRarity.Rare, 2, 0), dice));
        Assert.Throws<ArgumentException>(() => generator.Generate(
            new ItemGenerationRequest("item", "base", ItemRarity.Common, 1, 100), dice));
        Assert.Empty(dice.Trace.Rolls);
    }

    /// <summary>Higher power unlocks tiers without eliminating inexpensive compatible families.</summary>
    [Fact]
    public void HighPowerKeepsCheapFamiliesEligibleAndWeightIsIndependent()
    {
        var generator = ItemTestContent.CreateGenerator(
        [
            ItemTestContent.CreateAffix("a-cheap", weight: 3),
            ItemTestContent.CreateAffix("b-expensive", power: 20, weight: 1)
        ]);
        var request = new ItemGenerationRequest("item", "base", ItemRarity.Uncommon, 1, 100);

        var cheap = generator.Generate(request, new SequenceDiceRoller(3, 100));
        var expensive = generator.Generate(request, new SequenceDiceRoller(4, 1));

        Assert.Equal("a-cheap", Assert.Single(cheap.Affixes).DefinitionId);
        Assert.Equal("b-expensive", Assert.Single(expensive.Affixes).DefinitionId);
    }

    /// <summary>Selecting a family removes all its alternative definitions from the remaining pool.</summary>
    [Fact]
    public void GenerationExcludesTheWholeSelectedFamily()
    {
        var generator = ItemTestContent.CreateGenerator(
        [
            ItemTestContent.CreateAffix("a-health", family: "health"),
            ItemTestContent.CreateAffix("b-health", family: "health"),
            ItemTestContent.CreateAffix("c-mana", family: "mana")
        ]);

        var item = generator.Generate(new ItemGenerationRequest("item", "base", ItemRarity.Rare, 2, 0),
            new SequenceDiceRoller(1, 100, 1));

        Assert.Equal(new[] { "health", "mana" }, item.Affixes.Select(affix => affix.FamilyId));
    }

    /// <summary>The tier policy is explicit; equal power requirements are resolved deterministically.</summary>
    [Fact]
    public void TierPoliciesCanChooseHighestOrRetainLowerTiers()
    {
        var ranges = new[] { new AffixModifierRange(new StatKey(StatType.MaxMana), StatOperation.Flat, 1, 10) };
        var definition = new AffixDefinition("mana", "mana", 1,
        [
            new AffixTier("low", 0, ranges),
            new AffixTier("high", 10, ranges)
        ]);
        var request = new ItemGenerationRequest("item", "base", ItemRarity.Rare, 1, 10);

        var highest = ItemTestContent.CreateGenerator([definition]).Generate(request, new SequenceDiceRoller(100));
        var lower = ItemTestContent.CreateGenerator([definition], AffixTierSelection.UniformEligible)
            .Generate(request, new SequenceDiceRoller(2, 100));

        Assert.Equal("high", Assert.Single(highest.Affixes).TierId);
        Assert.Equal("low", Assert.Single(lower.Affixes).TierId);
    }

    /// <summary>Quality interpolation reaches both endpoints and rounds at the precision stored in content.</summary>
    /// <param name="quality">The shared d100 roll.</param>
    /// <param name="expected">The corresponding flat amount.</param>
    [Theory]
    [InlineData(1, 10)]
    [InlineData(50, 15)]
    [InlineData(100, 20)]
    public void QualityRollUsesInclusiveEndpointsAndConfiguredPrecision(int quality, int expected)
    {
        var affix = ItemTestContent.Roll(ItemTestContent.CreateAffix("health"), "owned", quality);

        Assert.Equal(quality, affix.QualityRoll);
        Assert.Equal(expected, Assert.Single(affix.Modifiers).Amount);
    }

    /// <summary>Fractional modifiers stay within their interval and round on both sides of a precision boundary.</summary>
    /// <param name="minimum">The inclusive fractional low endpoint.</param>
    /// <param name="maximum">The inclusive fractional high endpoint.</param>
    /// <param name="precision">The retained decimal places.</param>
    /// <param name="quality">The shared d100 result.</param>
    /// <param name="expected">The expected concrete fractional amount.</param>
    [Theory]
    [InlineData("0.1", "0.2", 2, 1, "0.1")]
    [InlineData("0.1", "0.2", 2, 100, "0.2")]
    [InlineData("0.1", "0.2", 2, 44, "0.14")]
    [InlineData("0.1", "0.2", 2, 46, "0.15")]
    [InlineData("-0.2", "-0.1", 2, 44, "-0.16")]
    [InlineData("-0.2", "-0.1", 2, 46, "-0.15")]
    [InlineData("0", "0.1", 1, 50, "0")]
    [InlineData("0", "0.1", 1, 51, "0.1")]
    public void FractionalQualityValuesRespectPrecisionAndBounds(string minimum, string maximum, int precision,
        int quality, string expected)
    {
        var low = decimal.Parse(minimum, CultureInfo.InvariantCulture);
        var high = decimal.Parse(maximum, CultureInfo.InvariantCulture);
        var definition = new AffixDefinition("health", "health", 1,
        [
            new AffixTier("first", 0,
            [
                new AffixModifierRange(new StatKey(StatType.MaxHealth), StatOperation.Increased, low, high, precision)
            ])
        ]);

        var affix = ItemTestContent.Roll(definition, "owned", quality);
        var amount = Assert.Single(affix.Modifiers).Amount;

        Assert.Equal(decimal.Parse(expected, CultureInfo.InvariantCulture), amount);
        Assert.InRange(amount, low, high);
    }

    /// <summary>Empty explicit-rarity generation works with no affix pool and consumes no random values.</summary>
    [Fact]
    public void EmptyLegendaryGenerationNeedsNoAffixPoolOrRolls()
    {
        var dice = new SequenceDiceRoller();
        var generator = ItemTestContent.CreateGenerator([]);

        var item = generator.Generate(new ItemGenerationRequest("item", "base", ItemRarity.Legendary, 0, 0), dice);

        Assert.Equal(ItemRarity.Legendary, item.Rarity);
        Assert.Empty(item.Affixes);
        Assert.Empty(dice.Trace.Rolls);
    }

    /// <summary>Overflowing selection weights fail before consuming randomness.</summary>
    [Fact]
    public void WeightOverflowIsRejectedBeforeRolling()
    {
        var generator = ItemTestContent.CreateGenerator(
        [
            ItemTestContent.CreateAffix("one", weight: int.MaxValue),
            ItemTestContent.CreateAffix("two", weight: 1)
        ]);
        var dice = new SequenceDiceRoller();

        Assert.Throws<OverflowException>(() => generator.Generate(
            new ItemGenerationRequest("item", "base", ItemRarity.Rare, 1, 0), dice));
        Assert.Empty(dice.Trace.Rolls);
    }

    /// <summary>An unknown tier is rejected before its d100 quality roll.</summary>
    [Fact]
    public void MissingTierDoesNotConsumeQualityRandomness()
    {
        var dice = new SequenceDiceRoller();

        Assert.Throws<KeyNotFoundException>(() => new AffixRoller().Roll("owned",
            ItemTestContent.CreateAffix("health"), "missing", dice));
        Assert.Empty(dice.Trace.Rolls);
    }
}
