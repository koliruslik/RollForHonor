using RollForHonor.Domain.Items.Models;
using RollForHonor.Domain.Rewards;
using RollForHonor.Domain.Tests.Items;

namespace RollForHonor.Domain.Tests.Rewards;

/// <summary>Verifies normalized Luck, capped tier bonuses and separate d100 gold amounts.</summary>
public sealed class GenerationLuckTests
{
    /// <summary>Tier bonuses change only at multiples of five and never exceed five.</summary>
    /// <param name="luck">The captured stat input.</param>
    /// <param name="bonus">The expected capped tier bonus.</param>
    [Theory]
    [InlineData(-10, 0)]
    [InlineData(0, 0)]
    [InlineData(4, 0)]
    [InlineData(5, 1)]
    [InlineData(24, 4)]
    [InlineData(25, 5)]
    [InlineData(1000, 5)]
    public void TierBonusUsesTheApprovedSteps(decimal luck, int bonus)
    {
        Assert.Equal(bonus, new GenerationLuck(luck).AffixTierBonus);
    }

    /// <summary>Quality and gold retain fractional Luck and cap safely even at the largest decimal input.</summary>
    [Fact]
    public void D100BoostDoesNotRoundOrOverflow()
    {
        Assert.Equal(0, new GenerationLuck(decimal.MinValue).Value);
        Assert.Equal(1, new GenerationLuck(-10).BoostD100(1));
        Assert.Equal(10.5m, new GenerationLuck(9.5m).BoostD100(1));
        Assert.Equal(100, new GenerationLuck(decimal.MaxValue).BoostD100(1));
        Assert.Equal(5, new GenerationLuck(decimal.MaxValue).AffixTierBonus);
        Assert.Throws<ArgumentOutOfRangeException>(() => new GenerationLuck(0).BoostD100(0));
        Assert.Throws<ArgumentOutOfRangeException>(() => new GenerationLuck(0).BoostD100(101));
    }

    /// <summary>Each gold result captures the natural d100 and effective amount independently of group-selection bonus.</summary>
    [Fact]
    public void GoldLuckCannotChooseAnotherRangeOrEquipmentRarity()
    {
        var group = new LootGroupDefinition("gold", new(1, 20),
        [
            new(1, LootOutcome.GoldEntry(new GoldRange(1, 10))),
            new(25, LootOutcome.GoldEntry(new GoldRange(101, 500)))
        ]);
        var dice = new SequenceDiceRoller(20, 1);
        var result = Assert.Single(new LootGroupRoller().Roll([group], 0, dice, luck: 100));

        Assert.Equal(10, result.GoldAmount);
        Assert.Equal(100, result.Luck.Value);
        Assert.Equal(0, result.LuckBonus);
        Assert.Equal(1, result.QuantityRoll!.Total);
        Assert.Equal(100, result.QuantityRoll.Formula.Sides);
        Assert.Equal(100, result.EffectiveQuantityRoll);
        Assert.Equal(20, result.SelectionRoll.Total);
        Assert.Equal(2, dice.Trace.Rolls.Count);
    }

    /// <summary>Luck intentionally increases maximum-amount frequency without changing the selected range.</summary>
    [Fact]
    public void GoldD100DistributionIsExhaustivelyBounded()
    {
        var range = new GoldRange(6, 14);
        var zero = new GenerationLuck(0);
        var lucky = new GenerationLuck(10);
        var natural = Enumerable.Range(1, 100)
            .Select(face => range.AmountAt(zero.BoostD100(face)))
            .ToArray();
        var boosted = Enumerable.Range(1, 100)
            .Select(face => range.AmountAt(lucky.BoostD100(face)))
            .ToArray();

        Assert.Equal(951, natural.Sum());
        Assert.Equal(1, natural.Count(amount => amount == 14));
        Assert.Equal(11, boosted.Count(amount => amount == 14));
        Assert.All(boosted, amount => Assert.InRange(amount, 6, 14));
        Assert.All(Enumerable.Range(0, 100), index => Assert.True(boosted[index] >= natural[index]));
    }

    /// <summary>Equipment-only groups do not gain extra dice or change their result from amount Luck.</summary>
    [Fact]
    public void AmountLuckDoesNotChangeEquipmentPoolOrRarity()
    {
        var outcome = LootOutcome.EquipmentEntry("bows", ItemRarity.Rare);
        var group = new LootGroupDefinition("equipment", new(1, 20), [new(1, outcome)]);
        var dice = new SequenceDiceRoller(1);
        var result = Assert.Single(new LootGroupRoller().Roll([group], 0, dice, 100));

        Assert.Same(outcome, result.Outcome);
        Assert.Null(result.QuantityRoll);
        Assert.Null(result.EffectiveQuantityRoll);
        Assert.Single(dice.Trace.Rolls);
    }
}
