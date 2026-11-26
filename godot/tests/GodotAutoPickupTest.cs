using System;
using System.Linq;
using System.Threading.Tasks;
using Chickensoft.GoDotTest;
using Godot;
using RollForHonor.Domain.Combat.State.Models;
using RollForHonor.Domain.Items.Configuration;
using RollForHonor.Domain.Items.Models;
using RollForHonor.Domain.Items.Services;
using RollForHonor.Godot.Arena;
using RollForHonor.Godot.UI.Hud;
using Shouldly;

namespace RollForHonor.Godot.Tests;

/// <summary>Checks automatic gold collection, manual item pickup, mode switching and pause/range guards.</summary>
/// <param name="testScene">The live native test host.</param>
public sealed class GodotAutoPickupTest(Node testScene) : TestClass(testScene)
{
    private GameBootstrap Runtime => TestScene.GetTree().Root.GetChildren()
        .OfType<GameBootstrap>()
        .Single();

    /// <summary>Nearby gold is collected once while every item remains available for manual pickup.</summary>
    /// <returns>Completes after repeated native pickup cycles.</returns>
    [Test]
    public async Task CollectsOnlyGoldAndLeavesItemsForManualPickup()
    {
        var arena = Create();

        try
        {
            await Frames(3);
            var item = Item("automatic");
            arena.PlaceAdminItem(item).ShouldBeTrue();
            PlaceGold(arena, 12);
            await Frames(40);
            var owner = arena.Player.CombatantId;
            Runtime.CombatState.ReadEquipment(owner).OwnedItems.ContainsKey(item.Id).ShouldBeFalse();
            Runtime.CombatState.ReadGoldWallet(owner).Balance.ShouldBe(12);
            arena.Drops.Single().Item.ShouldBeSameAs(item);
            arena.GoldDrops.ShouldBeEmpty();
            await Frames(30);
            Runtime.CombatState.ReadGoldWallet(owner).Balance.ShouldBe(12);
            Runtime.CombatState.ReadEquipment(owner).OwnedItems.Count.ShouldBe(1);
            arena.PickUpItem(item.Id).ShouldBeTrue();
            Runtime.CombatState.ReadEquipment(owner).OwnedItems[item.Id].ShouldBeSameAs(item);
        }
        finally
        {
            arena.Free();
        }
    }

    /// <summary>A full bag does not stop automatic gold collection; large and small items stay untouched.</summary>
    /// <returns>Completes after the full-bag pickup attempts and a repeat cycle.</returns>
    [Test]
    public async Task FullBagStillCollectsGoldAndLeavesEveryItem()
    {
        var arena = Create();

        try
        {
            await Frames(3);
            var owner = arena.Player.CombatantId;

            for (var index = 0; index < 60; index++)
            {
                Runtime.CombatState.RegisterOwnedItem(owner, Item($"filler-{index}"));
            }

            var large = Factory().Create($"auto-large:{Guid.NewGuid():N}", new("large", "Large item", footprint: new(2, 2)), ItemRarity.Common);
            var small = Item("small");
            arena.PlaceAdminItem(large).ShouldBeTrue();
            arena.PlaceAdminItem(small).ShouldBeTrue();
            arena.Drops.Single(drop => drop.Item.Id == large.Id).Position = arena.Player.Position + Vector3.Right * 0.3f;
            arena.Drops.Single(drop => drop.Item.Id == small.Id).Position = arena.Player.Position + Vector3.Right * 0.5f;
            PlaceGold(arena, 9);
            await Frames(40);
            var equipment = Runtime.CombatState.ReadEquipment(owner);
            equipment.OwnedItems.ContainsKey(large.Id).ShouldBeFalse();
            equipment.OwnedItems.ContainsKey(small.Id).ShouldBeFalse();
            arena.Drops.Count.ShouldBe(2);
            Runtime.CombatState.ReadGoldWallet(owner).Balance.ShouldBe(9);
            await Frames(30);
            arena.Drops.Single(drop => drop.Item.Id == large.Id).Item.ShouldBeSameAs(large);
            Runtime.CombatState.ReadGoldWallet(owner).Balance.ShouldBe(9);
        }
        finally
        {
            arena.Free();
        }
    }

    /// <summary>Automatic collection obeys pause, range and defeat guards even with explicit host callbacks.</summary>
    /// <returns>Completes after out-of-range native cycles.</returns>
    [Test]
    public async Task DoesNotCollectWhilePausedOutOfRangeOrDefeated()
    {
        var arena = Create();
        var tree = TestScene.GetTree();
        var previousPause = tree.Paused;

        try
        {
            await Frames(3);
            PlaceGold(arena, 7);
            tree.Paused = true;
            arena._PhysicsProcess(0);
            Runtime.CombatState.ReadGoldWallet(arena.Player.CombatantId).Balance.ShouldBe(0);
            tree.Paused = false;
            var origin = arena.Player.Position;
            arena.Player.Position += Vector3.Right * 10;
            await Frames(30);
            arena.GoldDrops.Count.ShouldBe(1);
            Runtime.CombatState.ReadGoldWallet(arena.Player.CombatantId).Balance.ShouldBe(0);
            arena.Player.Position = origin;
            var actor = Runtime.CombatState.Read(arena.Player.CombatantId, arena.Player.CombatantId).Source;
            Runtime.CombatState.UpdateCombatant(new(actor.Id, 0, actor.MaxHealth, actor.Armor, actor.Evasion,
                actor.Resistances, actor.Effects, actor.Offense, actor.EvasionRating));
            await Frames(30);
            arena.GoldDrops.Count.ShouldBe(1);
            Runtime.CombatState.ReadGoldWallet(actor.Id).Balance.ShouldBe(0);
        }
        finally
        {
            tree.Paused = previousPause;
            arena.Free();
        }
    }

    /// <summary>Explicitly discarded equipment stays on the ground until a manual pickup requests it.</summary>
    /// <returns>Completes after enough cycles to expose an unwanted automatic reacquisition.</returns>
    [Test]
    public async Task ManualDiscardIsNotImmediatelyUndone()
    {
        var arena = Create();

        try
        {
            await Frames(3);
            var owner = arena.Player.CombatantId;
            var item = Item("discarded");
            Runtime.CombatState.RegisterOwnedItem(owner, item);
            arena.DiscardItem(item.Id).ShouldBeTrue();
            await Frames(40);
            Runtime.CombatState.ReadEquipment(owner).OwnedItems.ContainsKey(item.Id).ShouldBeFalse();
            arena.Drops.Single().Item.ShouldBeSameAs(item);
            arena.PickUpItem(item.Id).ShouldBeTrue();
            Runtime.CombatState.ReadEquipment(owner).OwnedItems[item.Id].ShouldBeSameAs(item);
        }
        finally
        {
            arena.Free();
        }
    }

    /// <summary>Ctrl+G and the nearby-menu toggle change gold mode without collecting items or firing while typing.</summary>
    /// <returns>Completes after native cycles in disabled and enabled modes.</returns>
    [Test]
    public async Task GoldToggleUsesCtrlGAndRespectsTextFocus()
    {
        var arena = Create();
        var input = new LineEdit();
        TestScene.AddChild(input);

        try
        {
            await Frames(3);
            var hud = arena.GetNode<ArenaHud>("Presentation");
            arena.AutoGoldPickupEnabled.ShouldBeTrue();
            using var toggle = new InputEventKey { PhysicalKeycode = Key.G, CtrlPressed = true, Pressed = true };
            hud._Input(toggle);
            arena.AutoGoldPickupEnabled.ShouldBeFalse();
            var item = Item("toggle");
            arena.PlaceAdminItem(item).ShouldBeTrue();
            PlaceGold(arena, 8);
            await Frames(30);
            Runtime.CombatState.ReadGoldWallet(arena.Player.CombatantId).Balance.ShouldBe(0);
            arena.Drops.Single().Item.ShouldBeSameAs(item);
            input.GrabFocus();
            hud._Input(toggle);
            arena.AutoGoldPickupEnabled.ShouldBeFalse();
            arena._UnhandledKeyInput(toggle);
            Runtime.CombatState.ReadEquipment(arena.Player.CombatantId).OwnedItems.ContainsKey(item.Id).ShouldBeFalse();
            input.ReleaseFocus();
            var button = hud.GetNode<Button>("Root/Design/NearbyLoot/Panel/AutoGold");
            button.EmitSignal(BaseButton.SignalName.Pressed);
            arena.AutoGoldPickupEnabled.ShouldBeTrue();
            await Frames(30);
            Runtime.CombatState.ReadGoldWallet(arena.Player.CombatantId).Balance.ShouldBe(8);
            arena.Drops.Single().Item.ShouldBeSameAs(item);
            arena._UnhandledKeyInput(new InputEventKey { PhysicalKeycode = Key.G, Pressed = true });
            Runtime.CombatState.ReadEquipment(arena.Player.CombatantId).OwnedItems[item.Id].ShouldBeSameAs(item);
            arena.AutoGoldPickupEnabled.ShouldBeTrue();
        }
        finally
        {
            input.Free();
            arena.Free();
        }
    }

    private GreyboxArena Create()
    {
        var arena = GD.Load<PackedScene>("res://Arena/RoundArena.tscn").Instantiate<GreyboxArena>();
        arena.SpawnSeed = 724;
        Runtime.AddChild(arena);
        arena.SelectStarterWeapon("dexterity-weapon-travel").ShouldBeTrue();
        arena.Player.SetPhysicsProcess(false);
        arena.Opponent.Controls.ThinkingEnabled = false;
        return arena;
    }

    private void PlaceGold(GreyboxArena arena, long amount)
    {
        var source = new CombatantId(Guid.NewGuid());
        Runtime.CombatState.AddCombatant(new(source, 0, 1, 0, 0, [], []));
        arena.PlaceGoldReward(source, amount, arena.Player.Position).ShouldBeTrue();
    }

    private static ItemFactory Factory() => new(ItemRarityRules.Default, ItemStatRules.Default);

    private static ItemInstance Item(string id) => Factory().Create($"auto:{Guid.NewGuid():N}:{id}", new(id, "Small item"), ItemRarity.Common);

    private async Task Frames(int count)
    {
        for (var index = 0; index < count; index++)
        {
            await TestScene.ToSignal(TestScene.GetTree(), SceneTree.SignalName.PhysicsFrame);
        }
    }
}
