using System;
using Chickensoft.GoDotTest;
using Godot;
using RollForHonor.Domain.Items.Models;
using RollForHonor.Godot.Content.Items;
using Shouldly;

namespace RollForHonor.Godot.Tests;

/// <summary>Guards repeated content snapshots against stale cached C# Resource wrappers during collection.</summary>
public sealed class GodotItemContentLifetimeTest : TestClass
{
    /// <summary>Creates content-lifetime checks.</summary>
    /// <param name="testScene">The engine test host.</param>
    public GodotItemContentLifetimeTest(Node testScene) : base(testScene)
    {
    }

    /// <summary>Every load owns and disposes its engine Resource while its copied Domain table survives collection.</summary>
    [Test]
    public void RepeatedRuleSnapshotsSurviveFinalizerCollections()
    {
        var first = ItemRuleContent.LoadRarityRules();

        for (var index = 0; index < 12; index++)
        {
            var rules = ItemRuleContent.LoadRarityRules();
            GC.Collect();
            GC.WaitForPendingFinalizers();
            rules.GetCapacity(ItemRarity.Rare).ShouldBe(first.GetCapacity(ItemRarity.Rare));
        }
    }
}
