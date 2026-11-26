using RollForHonor.Domain.Dice.Models;
using RollForHonor.Domain.Items.Models;
using RollForHonor.Domain.Rewards;
using RollForHonor.Domain.Tests.Items;

namespace RollForHonor.Domain.Tests.Rewards;

/// <summary>Verifies independent loot groups, threshold access and exact captured gold mapping.</summary>
public sealed class LootGroupTests
{
    /// <summary>Effective d100 endpoints and interior faces map to the confirmed integer gold formula.</summary>
    /// <param name="minimum">The inclusive range minimum.</param>
    /// <param name="maximum">The inclusive range maximum.</param>
    /// <param name="face">The effective quantity result.</param>
    /// <param name="expected">The expected whole amount.</param>
    [Theory]
    [InlineData(1, 10, 1, 1)]
    [InlineData(1, 10, 50, 5)]
    [InlineData(1, 10, 100, 10)]
    [InlineData(11, 100, 1, 11)]
    [InlineData(11, 100, 50, 55)]
    [InlineData(11, 100, 100, 100)]
    [InlineData(101, 500, 50, 298)]
    [InlineData(101, 500, 100, 500)]
    [InlineData(7, 7, 15, 7)]
    [InlineData(1, long.MaxValue, 100, long.MaxValue)]
    public void GoldRangesMapD100WithoutFractionalAmounts(long minimum, long maximum, int face, long expected)
    {
        Assert.Equal(expected, new GoldRange(minimum, maximum).AmountAt(face));
    }

    /// <summary>Optional equipment can fail independently while the all-gold group still yields currency.</summary>
    [Fact]
    public void EmptyEquipmentDoesNotSuppressGold()
    {
        var dice = new SequenceDiceRoller(1, 100, 1);
        var result = new LootGroupRoller().Roll(Groups(), 0, dice);

        Assert.Equal(2, result.Count);
        Assert.Equal(10, result[0].GoldAmount);
        Assert.True(result[1].Outcome.IsEmpty);
        Assert.Null(result[1].QuantityRoll);
        Assert.Equal(3, dice.Trace.Rolls.Count);
    }

    /// <summary>Every independent group may produce its own reward on the same defeat.</summary>
    [Fact]
    public void GoldAndEquipmentCanBothDrop()
    {
        var dice = new SequenceDiceRoller(1, 1, 20);
        var result = new LootGroupRoller().Roll(Groups(), 0, dice);

        Assert.Equal(1, result[0].GoldAmount);
        Assert.Equal("equipment", result[1].Outcome.ItemPoolId);
        Assert.Equal(ItemRarity.Common, result[1].Outcome.Rarity);
        Assert.Equal(3, dice.Trace.Rolls.Count);
    }

    /// <summary>A higher threshold becomes reachable on only the top die face when the Luck bonus first opens it.</summary>
    [Fact]
    public void HigherLootStartsWithOneEligibleFace()
    {
        var low = LootOutcome.GoldEntry(new GoldRange(1, 10));
        var high = LootOutcome.GoldEntry(new GoldRange(11, 100));
        var group = new LootGroupDefinition("gold", new DiceFormula(1, 20), [new(1, low), new(25, high)]);
        var unlocked = Enumerable.Range(1, 20)
            .Count(face => ReferenceEquals(group.Select(face + 5), high));

        Assert.Equal(1, unlocked);
        Assert.All(Enumerable.Range(1, 20), face => Assert.Same(low, group.Select(face + 4)));
        var dice = new SequenceDiceRoller(20, 1);
        var captured = Assert.Single(new LootGroupRoller().Roll([group], 5, dice));
        Assert.Same(high, captured.Outcome);
        Assert.Equal(11, captured.GoldAmount);
        Assert.Equal(1, captured.QuantityRoll!.Total);
    }

    /// <summary>Invalid or duplicate groups fail before any randomness is consumed.</summary>
    [Fact]
    public void DuplicateGroupIdentitiesCannotConsumeRolls()
    {
        var group = Groups()[0];
        var dice = new SequenceDiceRoller();

        Assert.Throws<ArgumentException>(() => new LootGroupRoller().Roll([group, group], 0, dice));
        Assert.Empty(dice.Trace.Rolls);
        Assert.Throws<ArgumentException>(() => new LootGroupDefinition("gold", new(1, 20), [new(2, LootOutcome.Nothing())]));
        Assert.Throws<ArgumentException>(() => new LootGroupDefinition("gold", new(1, 20), [new(1, LootOutcome.Nothing()), new(1, LootOutcome.Nothing())]));
    }

    /// <summary>Invalid gold bounds and out-of-range quantity faces cannot become rewards.</summary>
    [Fact]
    public void InvalidGoldRangesAndFacesAreRejected()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new GoldRange(0, 10));
        Assert.Throws<ArgumentOutOfRangeException>(() => new GoldRange(11, 10));
        Assert.Throws<ArgumentOutOfRangeException>(() => new GoldRange(1, 10).AmountAt(0));
        Assert.Throws<ArgumentOutOfRangeException>(() => new GoldRange(1, 10).AmountAt(101));
    }

    private static LootGroupDefinition[] Groups() =>
    [
        new("gold", new(1, 20), [new(1, LootOutcome.GoldEntry(new GoldRange(1, 10)))]),
        new("equipment", new(1, 20), [new(1, LootOutcome.Nothing()), new(20, LootOutcome.EquipmentEntry("equipment", ItemRarity.Common))])
    ];
}
