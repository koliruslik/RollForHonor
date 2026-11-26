using RollForHonor.Domain.Items.Affixes.Models;
using RollForHonor.Domain.Items.Affixes.Services;
using RollForHonor.Domain.Items.Models;
using RollForHonor.Domain.Stats;

namespace RollForHonor.Domain.Tests.Items;

/// <summary>Verifies separate tier/quality Luck effects, eligibility gates and immutable affix facts.</summary>
public sealed class ItemLuckGenerationTests
{
    /// <summary>Generated affixes retain natural/effective tier and quality dice with the captured Luck snapshot.</summary>
    [Fact]
    public void AffixCapturesBothDiceAndLuck()
    {
        var generator = ItemTestContent.CreateGenerator([Definition()]);
        var dice = new SequenceDiceRoller(20, 1);
        var item = generator.GenerateWithLuck(new("item", "base", ItemRarity.Rare, 1, 30, itemLevel: 10), 5, dice);
        var affix = Assert.Single(item.Affixes);

        Assert.Equal("strong", affix.TierId);
        Assert.Equal(20, affix.RollContext.TierRoll!.Total);
        Assert.Equal(21, affix.RollContext.EffectiveTierRoll);
        Assert.Equal(5, affix.RollContext.Luck.Value);
        Assert.Equal(1, affix.QualityRoll);
        Assert.Equal(6, affix.EffectiveQualityRoll);
        Assert.Equal(101, Assert.Single(affix.Modifiers).Amount);
        Assert.Equal(new[] { 20, 100 }, dice.Trace.Rolls.Select(roll => roll.Formula.Sides));
    }

    /// <summary>High Luck cannot bypass the item-level gate or the caller's existing eligibility cap.</summary>
    /// <param name="itemLevel">The item's independent eligibility level.</param>
    /// <param name="power">The externally supplied tier cap.</param>
    [Theory]
    [InlineData(1, 30)]
    [InlineData(10, 20)]
    public void LuckRespectsIndependentGates(int itemLevel, int power)
    {
        var generator = ItemTestContent.CreateGenerator([Definition()]);
        var item = generator.GenerateWithLuck(new("item", "base", ItemRarity.Rare, 1, power, itemLevel),
            100, new SequenceDiceRoller(20, 100));

        Assert.Equal("base", Assert.Single(item.Affixes).TierId);
    }

    /// <summary>Captured affixes and fixed values do not change when later generations use different Luck.</summary>
    [Fact]
    public void GeneratedValuesRemainSnapshots()
    {
        var definition = ItemTestContent.CreateAffix("health");
        var first = new AffixRoller().Roll("old", definition, "first", new SequenceDiceRoller(1), -5);
        var lucky = new AffixRoller().Roll("new", definition, "first", new SequenceDiceRoller(1), 99);

        Assert.Equal(10, Assert.Single(first.Modifiers).Amount);
        Assert.Equal(1, first.EffectiveQualityRoll);
        Assert.Equal(0, first.RollContext.Luck.Value);
        Assert.Equal(20, Assert.Single(lucky.Modifiers).Amount);
        Assert.Equal(1, lucky.QualityRoll);
        Assert.Equal(100, lucky.EffectiveQualityRoll);
        Assert.Null(lucky.RollContext.TierRoll);
        var fixedDefinition = new AffixDefinition("fixed", "fixed", 1,
            [new("fixed", 0, [new(new(StatType.Armor), StatOperation.Flat, 7, 7)])]);
        var fixedAffix = new AffixRoller().Roll("fixed", fixedDefinition, "fixed", new SequenceDiceRoller(1), 100);
        Assert.Equal(7, Assert.Single(fixedAffix.Modifiers).Amount);
    }

    /// <summary>Luck does not change selected family identities, weights or the explicit occupied count.</summary>
    [Fact]
    public void LuckDoesNotChangeFamilySelectionOrAffixCount()
    {
        var generator = ItemTestContent.CreateGenerator([ItemTestContent.CreateAffix("a"), ItemTestContent.CreateAffix("b")]);
        var request = new ItemGenerationRequest("item", "base", ItemRarity.Uncommon, 2, 30);
        var first = generator.GenerateWithLuck(request, 0, new SequenceDiceRoller(1, 1, 1, 1, 1));
        var second = generator.GenerateWithLuck(request, 100, new SequenceDiceRoller(1, 1, 1, 1, 1));

        Assert.Equal(2, first.Affixes.Count);
        Assert.Equal(first.Affixes.Select(affix => affix.FamilyId), second.Affixes.Select(affix => affix.FamilyId));
        Assert.Equal(first.Rarity, second.Rarity);
        Assert.Same(first.Base, second.Base);
    }

    /// <summary>Incomplete tier tables fail before consuming random values rather than implicitly rerolling a low face.</summary>
    [Fact]
    public void MissingBaselineTierFailsBeforeRolling()
    {
        var generator = ItemTestContent.CreateGenerator([ItemTestContent.CreateAffix("no-baseline", power: 5)]);
        var dice = new SequenceDiceRoller();

        Assert.Throws<InvalidOperationException>(() => generator.GenerateWithLuck(new("item", "base", ItemRarity.Rare, 1, 30), 100, dice));
        Assert.Empty(dice.Trace.Rolls);
    }

    /// <summary>Empty items retain their explicit rarity/count policy and consume no new Luck dice.</summary>
    [Fact]
    public void EmptyItemUsesNoAffixRolls()
    {
        var dice = new SequenceDiceRoller();
        var item = ItemTestContent.CreateGenerator([]).GenerateWithLuck(new("item", "base", ItemRarity.Legendary, 0, 30), 100, dice);

        Assert.Empty(item.Affixes);
        Assert.Equal(ItemRarity.Legendary, item.Rarity);
        Assert.Empty(dice.Trace.Rolls);
    }

    private static AffixDefinition Definition() => new("health", "health", 1,
    [
        new("base", 0, [new(new(StatType.MaxHealth), StatOperation.Flat, 10, 20)]),
        new("strong", 21, [new(new(StatType.MaxHealth), StatOperation.Flat, 100, 120)], requiredItemLevel: 10)
    ]);
}
