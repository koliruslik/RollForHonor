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

    /// <summary>Ten distinct, separated actors spawn once; ten deaths produce ten stable drops and unlimited free time.</summary>
    /// <returns>Completes after physics has spawned, defeated and replaced a full round.</returns>
    [Test]
    public async Task TenEnemiesCompleteOneRoundAndPreserveLootIntoTheNext()
    {
        var arena = Create();

        try
        {
            await Frames(3);
            StopThinking(arena);
            arena.Enemies.Count.ShouldBe(10);
            arena.RoundNumber.ShouldBe(1);
            arena.Enemies
                .Select(enemy => enemy.CombatantId)
                .Distinct()
                .Count()
                .ShouldBe(10);
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

            foreach (var enemy in arena.Enemies.Take(9))
            {
                Defeat(arena, enemy.CombatantId);
            }

            await Frames(2);
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
            var hud = arena.GetNode<ArenaHud>("Presentation");
            hud.GetNode<Panel>("Root/Design/RoundComplete").Visible.ShouldBeTrue();
            arena.Player.Position = new Vector3(35, 0.1f, -35);
            arena.StartNextRound().ShouldBeTrue();
            arena.StartNextRound().ShouldBeFalse();
            await Frames(3);
            StopThinking(arena);
            arena.RoundNumber.ShouldBe(2);
            arena.DefeatedCount.ShouldBe(0);
            arena.Enemies.Count.ShouldBe(10);
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
                var tile = panel.GetNode<Panel>($"Character/{types[index]}");
                tile.GetNode<Label>("Value").Text.ShouldBe((index + 2).ToString());
                tile.Position.X.ShouldBe(20 + index % 3 * 108);
                tile.Position.Y.ShouldBe(96 + index / 3 * 96);
            }

            panel.GetNodeOrNull("Character/Wisdom").ShouldBeNull();
            stats.Get(new StatKey(StatType.MaxHealth)).ShouldBe(100);
            TranslationServer.SetLocale("uk");
            panel.Refresh(stats, profile.BaseValues, new(id, 100, 100, 0, 0, [], []), 0);
            panel.GetNode<Panel>("Character/Strength").TooltipText.ShouldBe(L.Text("Strength"));
            panel.GetNode<Label>("Character/Luck/Value").Text.ShouldBe("6");
        }
        finally
        {
            panel.Free();
            TranslationServer.SetLocale(previous);
        }
    }

    /// <summary>Different seeds still fill the courtyard with ten unique, separated spawns.</summary>
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
                positions.Length.ShouldBe(10);
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
