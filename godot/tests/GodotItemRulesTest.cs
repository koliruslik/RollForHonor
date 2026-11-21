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
}
