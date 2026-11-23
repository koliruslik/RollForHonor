using RollForHonor.Domain.Items.Affixes.Models;
using RollForHonor.Domain.Items.Affixes.Services;
using RollForHonor.Domain.Stats;

namespace RollForHonor.Domain.Tests.Items;

/// <summary>Verifies that rolled affixes retain their own tier pool independently of rarity and later content changes.</summary>
public sealed class AffixInstanceTests
{
    /// <summary>The roll retains every authored tier, including tiers above the selected power.</summary>
    /// <param name="tierCount">The affix-specific number of tiers, with T1 strongest.</param>
    [Theory]
    [InlineData(16)]
    [InlineData(15)]
    [InlineData(4)]
    public void RollCapturesItsDefinitionTierPool(int tierCount)
    {
        var tiers = Enumerable.Range(1, tierCount)
            .Select(rank => new AffixTier($"T{rank}", tierCount - rank,
            [
                new AffixModifierRange(new StatKey(StatType.MaxHealth), StatOperation.Flat,
                    tierCount - rank + 1, tierCount - rank + 2)
            ]))
            .ToArray();
        var definition = new AffixDefinition("health", "health", 1, tiers);
        var dice = new SequenceDiceRoller(72);

        var affix = new AffixRoller().Roll("owned-affix", definition, $"T{tierCount}", dice);
        tiers[0] = new AffixTier("replacement", 0,
            [new AffixModifierRange(new StatKey(StatType.MaxHealth), StatOperation.Flat, 99, 100)]);

        Assert.Equal($"T{tierCount}", affix.TierId);
        Assert.Equal(Enumerable.Range(1, tierCount).Select(rank => $"T{rank}"), affix.AvailableTierIds);
        Assert.Throws<NotSupportedException>(() => ((IList<string>)affix.AvailableTierIds)[0] = "changed");
        Assert.Equal(72, affix.QualityRoll);
        Assert.Single(dice.Trace.Rolls);
        Assert.Equal(1m, Assert.Single(affix.Ranges).Minimum);
    }
}
