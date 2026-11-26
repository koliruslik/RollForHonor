using System;
using System.Linq;
using System.Threading.Tasks;
using Chickensoft.GoDotTest;
using Godot;
using RollForHonor.Application.Items;
using RollForHonor.Domain.Rewards;
using RollForHonor.Infrastructure.Dice;
using RollForHonor.Domain.Combat.State.Models;
using RollForHonor.Domain.Items.Configuration;
using RollForHonor.Domain.Items.Models;
using RollForHonor.Domain.Items.Services;
using RollForHonor.Godot.Arena;
using RollForHonor.Godot.UI.Hud;
using Shouldly;

namespace RollForHonor.Godot.Tests;

/// <summary>Verifies actual gold world placement, native pickup controls and inventory-independent credit.</summary>
public sealed class GodotGoldRewardTest : TestClass
{
    /// <summary>Creates engine gold checks.</summary>
    /// <param name="testScene">The live engine test host.</param>
    public GodotGoldRewardTest(Node testScene) : base(testScene)
    {
    }

    /// <summary>Gold stays in the world until its menu row is collected, credits once and occupies no inventory cells.</summary>
    /// <returns>Completes after arena physics and native loot controls initialize.</returns>
    [Test]
    public async Task CapturedGoldRequiresPickupAndCreditsOnce()
    {
        var arena = Create();

        try
        {
            await Frames(3);
            StopThinking(arena);
            var state = arena.GetParent<GameBootstrap>().CombatState;
            var source = new CombatantId(Guid.NewGuid());
            state.AddCombatant(new(source, 0, 1, 0, 0, [], []));
            var group = new LootGroupDefinition("gold", new(1, 20), [new(1, LootOutcome.GoldEntry(new GoldRange(25, 25)))]);
            var rolled = new LootGroupRoller().Roll([group], 0, new RandomDiceRoller(new Random(724)), 10).Single();
            arena.PlaceGoldReward(source, rolled.GoldAmount, arena.Player.Position, rolled).ShouldBeTrue();
            arena.PlaceGoldReward(source, 500, arena.Player.Position).ShouldBeFalse();
            var drop = arena.GoldDrops.Single();
            drop.Reward.Amount.ShouldBe(25);
            drop.Reward.RollContext.ShouldBeSameAs(rolled);
            state.ReadGoldWallet(arena.Player.CombatantId).Balance.ShouldBe(0);
            var hud = arena.GetNode<ArenaHud>("Presentation");
            hud.ToggleInventory();
            hud._Process(0);
            var before = hud.ReadBagLayout();
            var row = (Button)hud.FindChild($"GoldRow{source.Value:N}", true, false);
            row.Text.ShouldContain("25");
            var normalStyle = (StyleBoxFlat)row.GetThemeStylebox("normal");
            normalStyle.BorderColor.A.ShouldBeGreaterThan(0);
            normalStyle.BorderWidthLeft.ShouldBeGreaterThan(0);
            await Frames(2);
            var rowSize = row.Size;
            var minimumSize = row.GetCombinedMinimumSize();
            using var hover = new InputEventMouseMotion
            {
                Position = row.GetGlobalTransformWithCanvas() * (row.Size / 2)
            };
            row.GetViewport().PushInput(hover, true);
            row.GetDrawMode().ShouldBe(BaseButton.DrawMode.Hover);
            row.Size.ShouldBe(rowSize);
            row.GetCombinedMinimumSize().ShouldBe(minimumSize);
            var capturePath = OS.GetEnvironment("RFH_GOLD_CAPTURE_PATH");

            if (!string.IsNullOrWhiteSpace(capturePath))
            {
                await TestScene.ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
                TestScene.GetViewport().GetTexture().GetImage().SavePng(capturePath).ShouldBe(Error.Ok);
            }

            row.EmitSignal(Button.SignalName.Pressed);

            state.ReadGoldWallet(arena.Player.CombatantId).Balance.ShouldBe(25);
            drop.Reward.RollContext.ShouldBeSameAs(rolled);
            drop.Collected.ShouldBeTrue();
            arena.PickUpGold(drop.LootId).ShouldBeFalse();
            hud._Process(0);
            hud.ReadBagLayout().ShouldBeSameAs(before);
            ((Label)hud.FindChild("GoldBalance", true, false)).Text.ShouldContain("25");

            if (!string.IsNullOrWhiteSpace(capturePath))
            {
                await TestScene.ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
                var collectedPath = System.IO.Path.Combine(System.IO.Path.GetDirectoryName(capturePath),
                    System.IO.Path.GetFileNameWithoutExtension(capturePath) + "-collected.png");
                TestScene.GetViewport().GetTexture().GetImage().SavePng(collectedPath).ShouldBe(Error.Ok);
            }
        }
        finally
        {
            arena.Free();
        }
    }

    /// <summary>Unreachable and paused gold cannot credit the wallet or consume the captured reward.</summary>
    /// <returns>Completes after the world and player are initialized.</returns>
    [Test]
    public async Task GoldPickupChecksRangeAndPause()
    {
        var arena = Create();
        var previousPause = TestScene.GetTree().Paused;

        try
        {
            await Frames(3);
            StopThinking(arena);
            var state = arena.GetParent<GameBootstrap>().CombatState;
            var source = new CombatantId(Guid.NewGuid());
            state.AddCombatant(new(source, 0, 1, 0, 0, [], []));
            arena.PlaceGoldReward(source, 12, arena.Player.Position).ShouldBeTrue();
            var drop = arena.GoldDrops.Single();
            var position = drop.Position;
            drop.Position = arena.Player.Position + Vector3.Right * 4;
            arena.PickUpGold(drop.LootId).ShouldBeFalse();
            drop.Position = position;
            TestScene.GetTree().Paused = true;
            arena.PickUpGold(drop.LootId).ShouldBeFalse();
            TestScene.GetTree().Paused = previousPause;
            state.ReadGoldWallet(arena.Player.CombatantId).Balance.ShouldBe(0);
            state.ReadGoldReward(source).ShouldBeSameAs(drop.Reward);

            arena.PickUpGold(drop.LootId).ShouldBeTrue();
            state.ReadGoldWallet(arena.Player.CombatantId).Balance.ShouldBe(12);
        }
        finally
        {
            TestScene.GetTree().Paused = previousPause;
            arena.Free();
        }
    }

    /// <summary>A full bag still permits gold pickup and keeps all item rectangles unchanged.</summary>
    /// <returns>Completes after the arena and retained bag controls initialize.</returns>
    [Test]
    public async Task FullBagDoesNotBlockGoldPickup()
    {
        var arena = Create();

        try
        {
            await Frames(3);
            StopThinking(arena);
            var hud = arena.GetNode<ArenaHud>("Presentation");
            hud.ToggleInventory();
            var factory = new ItemFactory(ItemRarityRules.Default, ItemStatRules.Default);
            var full = false;

            for (var index = 0; index < 61; index++)
            {
                var item = factory.Create($"gold-test-filler-{index}", new ItemDefinition("small", "Small item"), ItemRarity.Common);

                if (hud.AcquireItem(item, recordAction: false) == ItemAcquisitionStatus.InventoryFull)
                {
                    full = true;
                    break;
                }
            }

            full.ShouldBeTrue();
            var state = arena.GetParent<GameBootstrap>().CombatState;
            var source = new CombatantId(Guid.NewGuid());
            state.AddCombatant(new(source, 0, 1, 0, 0, [], []));
            arena.PlaceGoldReward(source, 9, arena.Player.Position).ShouldBeTrue();
            hud._Process(0);
            var before = hud.ReadBagLayout();

            arena.PickUpNearest().ShouldBeTrue();

            state.ReadGoldWallet(arena.Player.CombatantId).Balance.ShouldBe(9);
            hud.ReadBagLayout().ShouldBeSameAs(before);
        }
        finally
        {
            arena.Free();
        }
    }

    /// <summary>Blocked ground placement retains the first captured amount and retries without credit or reroll.</summary>
    /// <returns>Completes after blocker removal becomes visible to physics.</returns>
    [Test]
    public async Task BlockedPlacementRetainsCapturedGoldForRetry()
    {
        var arena = Create();

        try
        {
            await Frames(3);
            StopThinking(arena);
            arena.Player.SetPhysicsProcess(false);
            var runtime = arena.GetParent<GameBootstrap>();
            var state = runtime.CombatState;
            var source = new CombatantId(Guid.NewGuid());
            state.AddCombatant(new(source, 0, 1, 0, 0, [], []));
            var blocker = new StaticBody3D { CollisionLayer = 4, CollisionMask = 0, Position = arena.Player.Position + Vector3.Up };
            blocker.AddChild(new CollisionShape3D { Shape = new BoxShape3D { Size = new Vector3(18, 4, 18) } });
            arena.AddChild(blocker);
            await Frames(2);
            arena.PlaceGoldReward(source, 15, arena.Player.Position).ShouldBeFalse();
            arena.PlaceGoldReward(source, 500, arena.Player.Position).ShouldBeFalse();
            var captured = state.ReadGoldReward(source);
            captured.Amount.ShouldBe(15);
            arena.GoldDrops.ShouldBeEmpty();
            state.ReadGoldWallet(arena.Player.CombatantId).Balance.ShouldBe(0);
            blocker.Free();
            await Frames(2);
            runtime._Process(1.1);
            arena._PhysicsProcess(0);

            arena.GoldDrops.Single().Reward.ShouldBeSameAs(captured);
            state.ReadGoldWallet(arena.Player.CombatantId).Balance.ShouldBe(0);
        }
        finally
        {
            arena.Free();
        }
    }

    /// <summary>Unselected gold keeps a compact outline before and after selecting a neighboring item.</summary>
    [Test]
    public void GoldFrameSurvivesSelectionChanges()
    {
        var arena = Create();
        var menu = new RollForHonor.Godot.UI.Inventory.NearbyLootPanel();
        var item = new RollForHonor.Godot.Gameplay.Items.WorldItemDrop();
        TestScene.AddChild(menu);

        try
        {
            var state = arena.GetParent<GameBootstrap>().CombatState;
            var source = new CombatantId(Guid.NewGuid());
            state.AddCombatant(new(source, 0, 1, 0, 0, [], []));
            arena.PlaceGoldReward(source, 12, arena.Player.Position).ShouldBeTrue();
            item.Configure(new ItemFactory(ItemRarityRules.Default, ItemStatRules.Default)
                .Create("frame-test", new("amulet", "Copper Amulet", ["amulet"]), ItemRarity.Common));
            var gold = arena.GoldDrops.Single();
            menu.Refresh([item], [gold]);
            var goldRow = (Button)menu.FindChild($"GoldRow{source.Value:N}", true, false);
            var itemRow = (Button)menu.FindChild("Loot0", true, false);
            var normal = (StyleBoxFlat)goldRow.GetThemeStylebox("normal");
            normal.BorderColor.A.ShouldBeGreaterThan(0);
            normal.BorderWidthLeft.ShouldBeGreaterThan(0);
            var minimum = goldRow.GetCombinedMinimumSize();
            goldRow.EmitSignal(Control.SignalName.MouseEntered);
            menu.Refresh([item], [gold]);
            itemRow.EmitSignal(Control.SignalName.MouseEntered);
            menu.Refresh([item], [gold]);
            normal = (StyleBoxFlat)goldRow.GetThemeStylebox("normal");
            normal.BorderColor.A.ShouldBeGreaterThan(0);
            goldRow.GetCombinedMinimumSize().ShouldBe(minimum);
        }
        finally
        {
            item.Free();
            menu.Free();
            arena.Free();
        }
    }

    private GreyboxArena Create()
    {
        var runtime = TestScene.GetTree().Root.GetChildren()
            .OfType<GameBootstrap>()
            .Single();
        var arena = GD.Load<PackedScene>("res://Arena/RoundArena.tscn").Instantiate<GreyboxArena>();
        arena.SpawnSeed = 724;
        arena.AutoGoldPickupEnabled = false;
        runtime.AddChild(arena);
        arena.SelectStarterWeapon("dexterity-weapon-travel");
        arena.Player.Controls.HardwareInput = false;
        arena.Opponent.Controls.ThinkingEnabled = false;

        return arena;
    }

    private static void StopThinking(GreyboxArena arena)
    {
        foreach (var enemy in arena.Enemies)
        {
            enemy.Controls.ThinkingEnabled = false;
        }
    }

    private async Task Frames(int count)
    {
        for (var index = 0; index < count; index++)
        {
            await TestScene.ToSignal(TestScene.GetTree(), SceneTree.SignalName.PhysicsFrame);
        }
    }
}
