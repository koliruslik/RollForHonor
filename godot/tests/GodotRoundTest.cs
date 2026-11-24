using System;
using System.Linq;
using System.Threading.Tasks;
using Chickensoft.GoDotTest;
using Godot;
using RollForHonor.Application.Items;
using RollForHonor.Application.Equipment.Services;
using RollForHonor.Domain.Items.Configuration;
using RollForHonor.Domain.Items.Models;
using RollForHonor.Domain.Items.Services;
using RollForHonor.Domain.Items.Equipment.Models;
using RollForHonor.Domain.Stats;
using RollForHonor.Godot.Arena;
using RollForHonor.Godot.Gameplay.Stats;
using RollForHonor.Godot.UI.Hud;
using RollForHonor.Godot.UI.Inventory;
using RollForHonor.Godot.UI.Localization;
using Shouldly;

namespace RollForHonor.Godot.Tests;

/// <summary>Exercises real round spawning, defeat accounting, pickup capacity and primary-attribute presentation.</summary>
public sealed class GodotRoundTest : TestClass
{
    /// <summary>Creates checks in the native engine host.</summary>
    /// <param name="testScene">The active engine test scene.</param>
    public GodotRoundTest(Node testScene) : base(testScene)
    {
    }

    /// <summary>Five successive pairs produce ten stable drops and unlimited free time after the tenth defeat.</summary>
    /// <returns>Completes after physics has spawned, defeated and replaced a full round.</returns>
    [Test]
    public async Task PairsCompleteOneRoundAndPreserveLootIntoTheNext()
    {
        var arena = Create();

        try
        {
            await Frames(3);
            StopThinking(arena);
            arena.Enemies.Count.ShouldBe(2);
            arena.RoundNumber.ShouldBe(1);
            arena.Enemies
                .Select(enemy => enemy.CombatantId)
                .Distinct()
                .Count()
                .ShouldBe(2);
            var positions = arena.Enemies
                .Select(enemy => enemy.Position)
                .ToArray();
            positions.All(position => position.Y < 0.3f).ShouldBeTrue();
            positions
                .SelectMany((point, index) => positions
                    .Skip(index + 1)
                    .Select(other => point.DistanceTo(other)))
                .Min()
                .ShouldBeGreaterThan(2.9f);
            arena.StartNextRound().ShouldBeFalse();

            Defeat(arena, arena.Enemies[0].CombatantId);
            await Frames(2);
            arena.DefeatedCount.ShouldBe(1);
            arena.Enemies.Count.ShouldBe(2);

            for (var defeated = 1; defeated < 9; defeated++)
            {
                var enemy = arena.Enemies.First(value => !value.GetRequiredComponent<RollForHonor.Godot.Gameplay.Actors.ActorState>().IsDefeated);
                Defeat(arena, enemy.CombatantId);
                await Frames(2);
                StopThinking(arena);
                (arena.Enemies.Count - arena.DefeatedCount).ShouldBeLessThanOrEqualTo(2);
            }

            arena.DefeatedCount.ShouldBe(9);
            arena.RoundComplete.ShouldBeFalse();
            arena.Drops.Count.ShouldBe(9);
            Defeat(arena, arena.Enemies.Last().CombatantId);
            await Frames(3);
            arena.RoundComplete.ShouldBeTrue();
            arena.Drops.Count.ShouldBe(10);
            arena.Drops
                .Select(drop => drop.Item.Id)
                .Distinct()
                .Count()
                .ShouldBe(10);
            var items = arena.Drops
                .Select(drop => drop.Item)
                .ToArray();
            await Frames(8);
            arena.RoundNumber.ShouldBe(1);
            arena.Drops
                .Select(drop => drop.Item)
                .ShouldBe(items);
            arena.Enemies.Count.ShouldBe(10);
            var hud = arena.GetNode<ArenaHud>("Presentation");
            hud.GetNode<Panel>("Root/Design/RoundComplete").Visible.ShouldBeTrue();
            arena.Player.Position = new Vector3(35, 0.1f, -35);
            arena.StartNextRound().ShouldBeTrue();
            arena.StartNextRound().ShouldBeFalse();
            await Frames(3);
            StopThinking(arena);
            arena.RoundNumber.ShouldBe(2);
            arena.DefeatedCount.ShouldBe(0);
            arena.Enemies.Count.ShouldBe(2);
            arena.Drops
                .Select(drop => drop.Item)
                .ShouldBe(items);
        }
        finally
        {
            arena.Free();
        }
    }

    /// <summary>Full bags leave loot in the world; equipping an item frees space and permits exactly one pickup.</summary>
    /// <returns>Completes after a real drop has been created by a defeated actor.</returns>
    [Test]
    public async Task FullInventoryRetainsLootUntilSpaceIsFreed()
    {
        var arena = Create();

        try
        {
            await Frames(3);
            StopThinking(arena);
            Defeat(arena, arena.Opponent.CombatantId);
            await Frames(2);
            var drop = arena.Drops.Single();
            var item = drop.Item;
            var hud = arena.GetNode<ArenaHud>("Presentation");
            var factory = new ItemFactory(ItemRarityRules.Default, ItemStatRules.Default);

            for (var index = 0; index < 60; index++)
            {
                var filler = factory.Create($"round-capacity-{index}", new ItemDefinition("filler", "Filler"), ItemRarity.Common);

                if (hud.AcquireItem(filler) == ItemAcquisitionStatus.InventoryFull)
                {
                    break;
                }
            }

            arena.Player.GlobalPosition = drop.GlobalPosition;
            arena.PickUpNearest().ShouldBeFalse();
            hud._Process(0);
            hud.GetNode<Panel>("Root/Design/InventoryFullNotice").Visible.ShouldBeTrue();
            drop.Collected.ShouldBeFalse();
            drop.Item.ShouldBeSameAs(item);
            var state = arena.GetParent<GameBootstrap>().CombatState;
            var bow = state.ReadEquipment(arena.Player.CombatantId).OwnedItems.Values
                .Single(owned => owned.Base.Id == "ashen-longbow");
            new EquipmentService(state).EquipItem(arena.Player.CombatantId, bow.Id, EquipmentSlot.Weapon);
            arena.PickUpNearest().ShouldBeTrue();
            drop.Collected.ShouldBeTrue();
            state.ReadEquipment(arena.Player.CombatantId).OwnedItems[item.Id].ShouldBeSameAs(item);
            drop.TryCollect(_ => throw new InvalidOperationException("A collected item must not publish twice.")).ShouldBeFalse();
        }
        finally
        {
            arena.Free();
        }
    }

    /// <summary>Five actual primary values occupy three columns and two rows and survive a language change.</summary>
    [Test]
    public void PrimaryAttributesUseFiveRealValuesInATwoRowGrid()
    {
        var previous = TranslationServer.GetLocale();
        var panel = new CharacterPanel();
        TestScene.AddChild(panel);

        try
        {
            var definition = new ActorStatDefinition { Strength = 2, Dexterity = 3, Constitution = 4, Intelligence = 5, Luck = 6, MaxHealth = 100 };
            var profile = definition.CreateProfile();
            var stats = new StatCalculator().Calculate(profile);
            var id = new RollForHonor.Domain.Combat.State.Models.CombatantId(Guid.NewGuid());
            panel.Refresh(stats, profile.BaseValues, new(id, 100, 100, 0, 0, [], []), 0);
            var types = new[] { StatType.Strength, StatType.Dexterity, StatType.Constitution, StatType.Intelligence, StatType.Luck };

            for (var index = 0; index < types.Length; index++)
            {
                var tile = panel.GetNode<PrimaryAttributeDie>($"Character/{types[index]}");
                tile.GetNode<Label>("Value").Text.ShouldBe((index + 2).ToString());
                tile.Position.X.ShouldBe(index < 3 ? 20 + index * 108 : 74 + (index - 3) * 108);
                tile.Position.Y.ShouldBe(index < 3 ? 96 : 174);
            }

            RollForHonor.Godot.UI.Common.InspectionTopic inspected = null;
            panel.InspectionRequested += topic => inspected = topic;
            panel.GetNode<PrimaryAttributeDie>("Character/Strength")._GuiInput(new InputEventMouseButton { ButtonIndex = MouseButton.Left, Pressed = true });
            inspected.Title.ShouldBe(L.Text("Strength"));
            inspected.Description.ShouldContain("physical damage");
            panel.GetNodeOrNull("Character/Wisdom").ShouldBeNull();
            stats.Get(new StatKey(StatType.MaxHealth)).ShouldBe(100);
            TranslationServer.SetLocale("uk");
            panel.Refresh(stats, profile.BaseValues, new(id, 100, 100, 0, 0, [], []), 0);
            panel.GetNode<PrimaryAttributeDie>("Character/Strength").TooltipText.ShouldBe(L.Text("Strength"));
            panel.GetNode<Label>("Character/Luck/Value").Text.ShouldBe("6");
        }
        finally
        {
            panel.Free();
            TranslationServer.SetLocale(previous);
        }
    }

    /// <summary>Different seeds place two unique, separated spawns in the courtyard.</summary>
    /// <returns>Completes after multiple native physics spawn passes.</returns>
    [Test]
    public async Task SpawnSeedsProduceDifferentValidRounds()
    {
        Vector3[] previous = null;

        foreach (var seed in new[] { 1, 17, 983, 2026 })
        {
            var arena = Create(seed);

            try
            {
                await Frames(3);
                StopThinking(arena);
                var positions = arena.Enemies
                    .Select(enemy => enemy.Position)
                    .ToArray();
                positions.Length.ShouldBe(2);
                positions.All(point => point.Y < 0.3f).ShouldBeTrue();
                positions
                    .SelectMany((point, index) => positions
                        .Skip(index + 1)
                        .Select(other => point.DistanceTo(other)))
                    .Min()
                    .ShouldBeGreaterThan(2.9f);

                if (previous is not null)
                {
                    positions.SequenceEqual(previous).ShouldBeFalse();
                }

                previous = positions;
            }
            finally
            {
                arena.Free();
            }
        }
    }

    /// <summary>Real card discard, selected-G pickup and exact bag drops preserve identities and reject stale range.</summary>
    /// <returns>Completes after native menu and inventory controls have refreshed.</returns>
    [Test]
    public async Task NearbyLootSupportsDiscardSelectionAndExactBagDragging()
    {
        var arena = Create();

        try
        {
            await Frames(3);
            StopThinking(arena);
            var hud = arena.GetNode<ArenaHud>("Presentation");
            var state = arena.GetParent<GameBootstrap>().CombatState;
            var owner = arena.Player.CombatantId;
            hud.ToggleInventory();
            var grid = (InventoryGridView)hud.FindChild("Grid", true, false);
            var bow = state.ReadEquipment(owner).OwnedItems.Values.Single(item => item.Base.Id == "ashen-longbow");
            var oldPosition = grid.Layout.Placements[bow.Id].Position;
            var bowCell = (ItemCell)hud.FindChild("ashen-longbow", true, false);
            bowCell.EmitSignal(Button.SignalName.Pressed);
            hud.GetNode<Button>("Root/Design/InventoryOverlay/ItemDetails/Card/Discard").EmitSignal(Button.SignalName.Pressed);
            state.ReadEquipment(owner).OwnedItems.ContainsKey(bow.Id).ShouldBeFalse();
            arena.Drops.Single().Item.ShouldBeSameAs(bow);
            var ring = state.ReadEquipment(owner).OwnedItems.Values.First(item => item.Base.Footprint.Width == 1 && item.Base.Footprint.Height == 1);
            arena.DiscardItem(ring.Id).ShouldBeTrue();
            await Frames(2);
            hud._Process(0);
            var panel = hud.GetNode<NearbyLootPanel>("Root/Design/NearbyLoot");
            panel.Visible.ShouldBeTrue();
            var entries = panel.FindChildren("*", "Button", true, false)
                .OfType<LootEntry>()
                .ToArray();
            entries.Length.ShouldBe(2);
            entries.Single(entry => entry.Item.Id == ring.Id).EmitSignal(Control.SignalName.MouseEntered);
            arena._UnhandledKeyInput(new InputEventKey { PhysicalKeycode = Key.G, Pressed = true });
            state.ReadEquipment(owner).OwnedItems[ring.Id].ShouldBeSameAs(ring);
            arena.Drops.Single().Item.ShouldBeSameAs(bow);
            hud._Process(0);
            var payload = new global::Godot.Collections.Dictionary { ["rfh-item-id"] = bow.Id, ["rfh-grab-cell"] = Vector2I.Zero };
            var occupied = grid.Layout.Placements.Values.First().Position;
            var blockedPoint = new Vector2(occupied.Column, occupied.Row) * InventoryGridView.CellSize + Vector2.One;
            grid._CanDropData(blockedPoint, payload).ShouldBeFalse();
            grid._DropData(blockedPoint, payload);
            arena.Drops.Count.ShouldBe(1);
            var point = new Vector2(oldPosition.Column, oldPosition.Row) * InventoryGridView.CellSize + Vector2.One;
            var playerPosition = arena.Player.Position;
            arena.Player.Position += new Vector3(20, 0, 0);
            grid._CanDropData(point, payload).ShouldBeFalse();
            arena.PickUpItem(bow.Id).ShouldBeFalse();
            arena.Player.Position = playerPosition;
            grid._CanDropData(point, payload).ShouldBeTrue();
            grid._DropData(point, payload);
            grid.Layout.Placements[bow.Id].Position.ShouldBe(oldPosition);
            state.ReadEquipment(owner).OwnedItems[bow.Id].ShouldBeSameAs(bow);
            arena.Drops.Count.ShouldBe(0);
            arena.DiscardItem(bow.Id).ShouldBeTrue();
            await Frames(2);
            hud._Process(0);
            panel.FindChildren("*", "Button", true, false)
                .OfType<LootEntry>()
                .Single()
                .EmitSignal(Button.SignalName.Pressed);
            state.ReadEquipment(owner).OwnedItems[bow.Id].ShouldBeSameAs(bow);
            arena.Drops.Count.ShouldBe(0);
        }
        finally
        {
            arena.Free();
        }
    }

    /// <summary>Spent mana returns during game time, pauses with the tree and stops after player defeat.</summary>
    /// <returns>Completes after the round host and projectile capability are initialized.</returns>
    [Test]
    public async Task ManaRecoveryUsesUnpausedGameTimeAndStopsAfterDefeat()
    {
        var arena = Create();
        var tree = TestScene.GetTree();
        var previousPause = tree.Paused;

        try
        {
            await Frames(3);
            StopThinking(arena);
            var runtime = arena.GetParent<GameBootstrap>();
            var owner = arena.Player.CombatantId;
            arena.Player.GetRequiredComponent<RollForHonor.Godot.Gameplay.Combat.ProjectileAttack>().RequestAttack(arena.Player.Position + Vector3.Right * 5 + Vector3.Up);
            decimal Mana() => runtime.CombatState.ReadForStart(owner, "arena-shot", Guid.NewGuid()).Resources.Single().Amount;
            var spent = Mana();
            spent.ShouldBeLessThan(500);
            runtime._Process(1);
            arena._PhysicsProcess(0);
            Mana().ShouldBeGreaterThan(spent);
            var time = runtime.GameTime;
            tree.Paused = true;
            runtime._Process(3);
            arena._PhysicsProcess(0);
            runtime.GameTime.ShouldBe(time);
            tree.Paused = false;
            runtime.CombatState.UpdateStats(owner, new ActorStatDefinition { MaxHealth = 100, MaxMana = 1000 }.CreateProfile());
            Defeat(arena, owner);
            var before = Mana();
            runtime._Process(2);
            arena._PhysicsProcess(0);
            Mana().ShouldBe(before);
        }
        finally
        {
            tree.Paused = previousPause;
            arena.Free();
        }
    }

    /// <summary>Live recovery inputs drive both resources; fractional health accumulates and cannot revive a defeated player.</summary>
    /// <returns>Completes after the native round starts.</returns>
    [Test]
    public async Task HealthRecoveryUsesFlatResolvedRatesAndPreservesFractions()
    {
        var arena = Create();

        try
        {
            await Frames(3);
            StopThinking(arena);
            var runtime = arena.GetParent<GameBootstrap>();
            var owner = arena.Player.CombatantId;
            var state = runtime.CombatState;
            var profile = state.ReadEquipment(owner).NonEquipmentProfile;
            state.UpdateNonEquipmentStats(owner, new(profile.BaseValues.With(new(StatType.HealthRecovery), 0.5m).With(new(StatType.ManaRecovery), 3), profile.Modifiers));
            var actor = state.Read(owner, owner).Source;
            state.UpdateCombatant(new(owner, 50, actor.MaxHealth, actor.Armor, actor.Evasion, actor.Resistances, actor.Effects, actor.Offense));
            runtime._Process(1);
            arena._PhysicsProcess(0);
            state.Read(owner, owner).Source.Health.ShouldBe(50);
            runtime._Process(1);
            arena._PhysicsProcess(0);
            state.Read(owner, owner).Source.Health.ShouldBe(51);
            Defeat(arena, owner);
            runtime._Process(10);
            arena._PhysicsProcess(0);
            state.Read(owner, owner).Source.Health.ShouldBe(0);
        }
        finally
        {
            arena.Free();
        }
    }

    /// <summary>The compact admin surface edits canonical bases, creates unique items and queues sandbox actors outside round accounting.</summary>
    /// <returns>Completes after queued physics spawning.</returns>
    [Test]
    public async Task AdminToolsEditStatsCreateItemsAndKeepRoundAccounting()
    {
        var arena = Create();

        try
        {
            await Frames(3);
            StopThinking(arena);
            var hud = arena.GetNode<ArenaHud>("Presentation");
            hud._Input(new InputEventKey { PhysicalKeycode = Key.F3, Pressed = true });
            var admin = hud.GetNode<RollForHonor.Godot.UI.Admin.AdminPanel>("Root/Design/Admin");
            admin.Visible.ShouldBeTrue();
            var state = arena.GetParent<GameBootstrap>().CombatState;
            var stat = admin.GetNode<OptionButton>("Panel/Tabs/Stats/Stat");
            var manaIndex = Enumerable.Range(0, stat.ItemCount).Single(index => stat.GetItemText(index) == "Mana recovery");
            stat.Select(manaIndex);
            stat.EmitSignal(OptionButton.SignalName.ItemSelected, manaIndex);
            admin.GetNode<SpinBox>("Panel/Tabs/Stats/Value").Value = 27.5;
            admin.GetNode<Button>("Panel/Tabs/Stats/Apply").EmitSignal(Button.SignalName.Pressed);
            state.GetStats(arena.Player.CombatantId).Get(new(StatType.ManaRecovery)).ShouldBe(27.5m);
            var count = state.ReadEquipment(arena.Player.CombatantId).OwnedItems.Count + arena.Drops.Count;
            admin.GetNode<Button>("Panel/Tabs/Items/Create").EmitSignal(Button.SignalName.Pressed);
            (state.ReadEquipment(arena.Player.CombatantId).OwnedItems.Count + arena.Drops.Count).ShouldBe(count + 1);
            admin.GetNode<Button>("Panel/Tabs/World/Spawn").EmitSignal(Button.SignalName.Pressed);
            await Frames(2);
            arena.AdminEnemies.Count.ShouldBe(1);
            arena.Enemies.Count.ShouldBe(2);
            arena.DefeatedCount.ShouldBe(0);
            arena.AdminEnemies[0].Controls.ThinkingEnabled = false;
            var journal = hud.GetNode<ActionJournalPanel>("Root/Design/History");
            var text = journal.GetNode<RichTextLabel>("Journal/Entries").GetParsedText();
            text.ShouldContain("Admin: created");
            text.ShouldContain("Admin: spawned");
            journal.LastRoll.ShouldBeNull();
            admin.GetNode<TabContainer>("Panel/Tabs").CurrentTab = 2;
            admin.GetNode<SpinBox>("Panel/Tabs/Stats/Value").GetLineEdit().GrabFocus();
            hud._Input(new InputEventKey { PhysicalKeycode = Key.F3, Pressed = true });
            admin.Visible.ShouldBeFalse();
        }
        finally
        {
            arena.Free();
        }
    }

    /// <summary>Native wheel input scrolls the character sheet without zooming; wheel input over the world still zooms.</summary>
    /// <returns>Completes after GUI hover and native input processing.</returns>
    [Test]
    public async Task CharacterWheelDoesNotZoomTheWorld()
    {
        var arena = Create();

        try
        {
            await Frames(3);
            StopThinking(arena);
            var hud = arena.GetNode<ArenaHud>("Presentation");
            hud.ToggleCharacter();
            var scroll = (ScrollContainer)hud.FindChild("StatsScroll", true, false);
            scroll.MouseForcePassScrollEvents.ShouldBeFalse();
            var point = scroll.GetGlobalTransformWithCanvas() * new Vector2(140, 140);
            Input.ParseInputEvent(new InputEventMouseMotion { Position = point });
            await Frames(2);
            var before = arena.CameraRig.Camera.Size;
            Input.ParseInputEvent(new InputEventMouseButton { Position = point, ButtonIndex = MouseButton.WheelDown, Pressed = true });
            await Frames(2);
            arena.CameraRig.Camera.Size.ShouldBe(before);

            var world = arena.CameraRig.Camera.GetViewport().GetVisibleRect().Size / 2;
            Input.ParseInputEvent(new InputEventMouseMotion { Position = world });
            await Frames(2);
            Input.ParseInputEvent(new InputEventMouseButton { Position = world, ButtonIndex = MouseButton.WheelDown, Pressed = true });
            await Frames(2);
            arena.CameraRig.Camera.Size.ShouldBeGreaterThan(before);
        }
        finally
        {
            arena.Free();
        }
    }

    /// <summary>Native world-drop callbacks release only bag items, preserve identity and reject a duplicate release.</summary>
    /// <returns>Completes after the live bag is initialized.</returns>
    [Test]
    public async Task DraggingBagItemsIntoWorldUsesCanonicalRelease()
    {
        var arena = Create();

        try
        {
            await Frames(3);
            StopThinking(arena);
            var hud = arena.GetNode<ArenaHud>("Presentation");
            hud.ToggleInventory();
            var owner = arena.Player.CombatantId;
            var state = arena.GetParent<GameBootstrap>().CombatState;
            var bow = state.ReadEquipment(owner).OwnedItems.Values.Single(item => item.Base.Id == "ashen-longbow");
            var target = hud.GetNode<WorldDropTarget>("Root/Design/WorldDropTarget");
            var payload = new global::Godot.Collections.Dictionary { ["rfh-item-id"] = bow.Id };
            target._CanDropData(Vector2.Zero, payload).ShouldBeTrue();
            target._DropData(Vector2.Zero, payload);
            arena.Drops.Single().Item.ShouldBeSameAs(bow);
            state.ReadEquipment(owner).OwnedItems.ContainsKey(bow.Id).ShouldBeFalse();
            target._CanDropData(Vector2.Zero, payload).ShouldBeFalse();
            target._DropData(Vector2.Zero, payload);
            arena.Drops.Count.ShouldBe(1);
            arena.PickUpItem(bow.Id).ShouldBeTrue();
            new EquipmentService(state).EquipItem(owner, bow.Id, EquipmentSlot.Weapon);
            target._CanDropData(Vector2.Zero, payload).ShouldBeFalse();
            state.ReadEquipment(owner).Loadout.GetItem(EquipmentSlot.Weapon).ShouldBeSameAs(bow);
        }
        finally
        {
            arena.Free();
        }
    }

    /// <summary>Hover cards remain on screen without covering the cursor or source across edges and aspect ratios.</summary>
    [Test]
    public void TooltipPlacementAvoidsTheHoveredSourceAcrossViewportEdges()
    {
        foreach (var bounds in new[] { new Vector2(1920, 1080), new Vector2(1280, 720), new Vector2(2560, 1080) })
        {
            foreach (var cursor in new[] { new Vector2(20, 20), bounds - new Vector2(20, 20), bounds / 2, new Vector2(bounds.X - 20, 30), new Vector2(30, bounds.Y - 20) })
            {
                var source = new Rect2(cursor - Vector2.One * 16, Vector2.One * 32);
                var size = new Vector2(320, 420);
                var point = RollForHonor.Godot.UI.Common.TooltipPlacement.Place(bounds, size, cursor, source);
                var tooltip = new Rect2(point, size);
                new Rect2(Vector2.Zero, bounds).Encloses(tooltip).ShouldBeTrue();
                tooltip.HasPoint(cursor).ShouldBeFalse();
                tooltip.Intersects(source).ShouldBeFalse();
            }
        }
    }

    /// <summary>General actions render on consecutive lines without blank separators or synthetic dice results.</summary>
    [Test]
    public void ActionJournalDoesNotInsertBlankLinesBetweenActions()
    {
        var journal = new ActionJournalPanel();
        TestScene.AddChild(journal);

        try
        {
            journal.RecordAction("First action");
            journal.RecordAction("Second action");
            var text = journal.GetNode<RichTextLabel>("Journal/Entries").GetParsedText();
            text.ShouldBe("Second action\nFirst action\n");
            text.ShouldNotContain("\n\n");
            journal.LastRoll.ShouldBeNull();
        }
        finally
        {
            journal.Free();
        }
    }

    private GreyboxArena Create(int seed = 724)
    {
        var runtime = TestScene.GetTree().Root.GetChildren().OfType<GameBootstrap>().Single();
        var arena = GD.Load<PackedScene>("res://Arena/RoundArena.tscn").Instantiate<GreyboxArena>();
        arena.SpawnSeed = seed;
        runtime.AddChild(arena);
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

    private static void Defeat(GreyboxArena arena, RollForHonor.Domain.Combat.State.Models.CombatantId id)
    {
        var state = arena.GetParent<GameBootstrap>().CombatState;
        var current = state.Read(id, id).Source;
        state.UpdateCombatant(new(id, 0, current.MaxHealth, current.Armor, current.Evasion, current.Resistances, current.Effects, current.Offense));
    }

    private async Task Frames(int count)
    {
        for (var index = 0; index < count; index++)
        {
            await TestScene.ToSignal(TestScene.GetTree(), SceneTree.SignalName.PhysicsFrame);
        }
    }
}
