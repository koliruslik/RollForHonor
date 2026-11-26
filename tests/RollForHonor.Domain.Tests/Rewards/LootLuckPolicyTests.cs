using RollForHonor.Domain.Items.Models;
using RollForHonor.Domain.Rewards;

namespace RollForHonor.Domain.Tests.Rewards;

/// <summary>Verifies the approved occurrence curve, rarity tails and exact single-roll boundaries.</summary>
public sealed class LootLuckPolicyTests
{
    /// <summary>The baseline distribution and approved Luck examples have exact representable face counts.</summary>
    [Theory]
    [InlineData(0, 550000, 1000)]
    [InlineData(10, 518083, 1100)]
    [InlineData(50, 407891, 1500)]
    [InlineData(100, 302500, 1999)]
    public void CurveMatchesApprovedExamples(decimal luck, int emptyFaces, int legendaryFaces)
    {
        var group = new LootLuckPolicy(100).Apply(Baseline(), new(luck));
        Assert.Equal(1_000_000, group.Dice.Sides);
        Assert.Equal(emptyFaces + 1, group.Bands[1].MinimumRoll);
        Assert.Equal(legendaryFaces, group.Dice.Sides - group.Bands[^1].MinimumRoll + 1);
        Assert.Equal(ItemRarity.Legendary, group.Select(group.Dice.Sides).Rarity);
    }

    /// <summary>Every base boundary retains its original probability at zero or negative Luck.</summary>
    [Theory]
    [InlineData(0)]
    [InlineData(-100)]
    public void ZeroLuckPreservesEveryBand(decimal luck)
    {
        var baseline = Baseline();
        var actual = new LootLuckPolicy(100).Apply(baseline, new(luck));

        for (var index = 0; index < baseline.Bands.Count; index++)
        {
            Assert.Equal((baseline.Bands[index].MinimumRoll - 1) * 1000 + 1, actual.Bands[index].MinimumRoll);
            Assert.Same(baseline.Bands[index].Outcome, actual.Bands[index].Outcome);
        }
    }

    /// <summary>Higher Luck cannot worsen any fixed roll, including either side of every changed threshold.</summary>
    [Theory]
    [InlineData(0, 10)]
    [InlineData(10, 50)]
    [InlineData(50, 100)]
    [InlineData(100, 1000000)]
    public void HigherLuckNeverWorsensASelection(decimal lowerLuck, decimal higherLuck)
    {
        var policy = new LootLuckPolicy(100);
        var lower = policy.Apply(Baseline(), new(lowerLuck));
        var higher = policy.Apply(Baseline(), new(higherLuck));
        var probes = lower.Bands.Concat(higher.Bands)
            .SelectMany(band => new[] { band.MinimumRoll, Math.Max(1, band.MinimumRoll - 1) })
            .Append(lower.Dice.Sides);

        foreach (var face in probes)
        {
            Assert.True(Rank(higher.Select(face)) >= Rank(lower.Select(face)));
        }
    }

    /// <summary>Fractional Luck is retained and extreme inputs collapse zero-width bands without duplicate thresholds.</summary>
    [Fact]
    public void FractionalAndExtremeLuckRemainValid()
    {
        var policy = new LootLuckPolicy(100);
        var fractional = policy.Apply(Baseline(), new(0.5m));
        Assert.True(fractional.Bands[1].MinimumRoll < 550001);
        var extreme = policy.Apply(Baseline(), new(decimal.MaxValue));
        Assert.Single(extreme.Bands);
        Assert.Equal(1, extreme.Bands[0].MinimumRoll);
        Assert.Equal(ItemRarity.Legendary, extreme.Select(1).Rarity);
    }

    /// <summary>Invalid precision or nonuniform dice are rejected before any selection can be made.</summary>
    [Fact]
    public void RejectsIncompatibleInputs()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new LootLuckPolicy(0));
        Assert.Throws<ArgumentOutOfRangeException>(() => new LootLuckPolicy(100, 1));
        Assert.Throws<ArgumentException>(() => new LootLuckPolicy(100, 1001).Apply(Baseline(), new(0)));
        var nonuniform = new LootGroupDefinition("equipment", new(2, 6), [new(1, LootOutcome.Nothing())]);
        Assert.Throws<ArgumentException>(() => new LootLuckPolicy(100).Apply(nonuniform, new(0)));
    }

    private static int Rank(LootOutcome outcome) => outcome.IsEmpty ? -1 : (int)outcome.Rarity!.Value;

    private static LootGroupDefinition Baseline() => new("equipment", new(1, 1000),
    [
        new(1, LootOutcome.Nothing()),
        new(551, LootOutcome.EquipmentEntry("sentinel", ItemRarity.Common)),
        new(801, LootOutcome.EquipmentEntry("sentinel", ItemRarity.Uncommon)),
        new(951, LootOutcome.EquipmentEntry("sentinel", ItemRarity.Rare)),
        new(991, LootOutcome.EquipmentEntry("sentinel", ItemRarity.Epic)),
        new(1000, LootOutcome.EquipmentEntry("sentinel", ItemRarity.Legendary))
    ]);
}
