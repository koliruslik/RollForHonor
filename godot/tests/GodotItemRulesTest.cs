using System;
using System.Linq;
using RollForHonor.Godot.Content.Items;
using RollForHonor.Godot.UI.Inventory;
using RollForHonor.Infrastructure.Dice;
using Chickensoft.GoDotTest;
using Godot;
using RollForHonor.Domain.Items.Models;
using RollForHonor.Godot.Gameplay.Items.Configuration;
using Shouldly;

namespace RollForHonor.Godot.Tests;

/// <summary>Verifies native item-rule content loading and immutable Domain capture.</summary>
public sealed class GodotItemRulesTest : TestClass
{
    /// <summary>Creates item-rule Resource checks in the engine test host.</summary>
    /// <param name="testScene">The active engine test host.</param>
    public GodotItemRulesTest(Node testScene) : base(testScene)
    {
    }

    /// <summary>Serialized capacities load correctly and editing a resource does not mutate previously captured rules.</summary>
    [Test]
    public void SerializedRarityRulesCanBeChangedWithoutMutatingExistingRules()
    {
        using var definition = ResourceLoader.Load<ItemRarityRulesDefinition>(
            "res://Items/ItemRarityRules.tres", cacheMode: ResourceLoader.CacheMode.Ignore);
        var original = definition.CreateRules();

        definition.LegendaryCapacity = 10;
        var edited = definition.CreateRules();

        original.GetCapacity(ItemRarity.Common).ShouldBe(0);
        original.GetCapacity(ItemRarity.Uncommon).ShouldBe(2);
        original.GetCapacity(ItemRarity.Rare).ShouldBe(4);
        original.GetCapacity(ItemRarity.Epic).ShouldBe(6);
        original.GetCapacity(ItemRarity.Legendary).ShouldBe(8);
        edited.GetCapacity(ItemRarity.Legendary).ShouldBe(10);
    }

    /// <summary>New fixture drops apply captured Luck while authored kit values and base selection stay unchanged.</summary>
    [Test]
    public void EnemyDropLuckPreservesAuthoredItemsAndBaseSelection()
    {
        var source = new RollForHonor.Domain.Combat.State.Models.CombatantId(Guid.NewGuid());
        var authored = StarterItemCatalog.Create(source).Single(item => item.Base.Id == "ashen-longbow");
        var ordinary = StarterItemCatalog.RollDrop(source, new RandomDiceRoller(new MinimumRandom()), 0);
        var lucky = StarterItemCatalog.RollDrop(source, new RandomDiceRoller(new MinimumRandom()), 100);

        lucky.Base.Id.ShouldBe(ordinary.Base.Id);
        lucky.Rarity.ShouldBe(ordinary.Rarity);
        lucky.Affixes.Count.ShouldBe(ordinary.Affixes.Count);
        ordinary.Affixes.All(affix => affix.EffectiveQualityRoll == 1).ShouldBeTrue();
        lucky.Affixes.All(affix => affix.QualityRoll == 1 && affix.EffectiveQualityRoll == 100).ShouldBeTrue();
        lucky.Affixes.All(affix => affix.RollContext.EffectiveTierRoll == 6).ShouldBeTrue();
        lucky.Affixes.First().Modifiers.Single().Amount.ShouldBe(3);
        ItemInspectionTopics.Affix(lucky.Rarity, lucky.Affixes.First(), true).Facts
            .Any(fact => fact.Text.Contains("100%", StringComparison.Ordinal))
            .ShouldBeTrue();
        authored.Affixes.First().QualityRoll.ShouldBe(72);
        authored.Affixes.First().EffectiveQualityRoll.ShouldBe(72);
        lucky.Base.Weapon.Damage.SequenceEqual(ordinary.Base.Weapon.Damage).ShouldBeTrue();
    }

    private sealed class MinimumRandom : Random
    {
        /// <inheritdoc />
        public override long NextInt64(long minValue, long maxValue) => minValue;
    }

}
