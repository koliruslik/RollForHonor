using System.Linq;
using System.Threading.Tasks;
using Chickensoft.GoDotTest;
using Godot;
using RollForHonor.Application.Equipment.Models;
using RollForHonor.Application.Equipment.Services;
using RollForHonor.Domain.Items.Configuration;
using RollForHonor.Domain.Items.Equipment.Models;
using RollForHonor.Domain.Items.Models;
using RollForHonor.Domain.Items.Services;
using RollForHonor.Domain.Stats;
using RollForHonor.Godot.Arena;
using RollForHonor.Godot.Gameplay.Movement;
using RollForHonor.Godot.Gameplay.Stats;
using Shouldly;

namespace RollForHonor.Godot.Tests;

/// <summary>Checks editable stat definitions and live shared-actor projection in the actual engine.</summary>
public sealed class GodotCharacterStatsTest : TestClass
{
    private GameBootstrap Runtime => TestScene.GetTree().Root.GetChildren()
        .OfType<GameBootstrap>()
        .Single();

    /// <summary>Creates engine-level stat checks.</summary>
    /// <param name="testScene">The host used to await native physics.</param>
    public GodotCharacterStatsTest(Node testScene) : base(testScene)
    {
    }

    /// <summary>Player and enemy register from separate resources through the same immutable Domain model.</summary>
    /// <returns>Completes after initial stat bridges have advanced.</returns>
    [Test]
    public async Task DefinitionsPreserveExistingPlayerAndEnemyValues()
    {
        var arena = Create();

        try
        {
            await Frames(3);
            var player = Runtime.CombatState.Read(arena.Player.CombatantId, arena.Opponent.CombatantId);

            player.Source.MaxHealth.ShouldBe(100);
            player.Source.Armor.ShouldBe(2);
            player.Source.Evasion.ShouldBe(12);
            player.Target.MaxHealth.ShouldBe(40);
            player.Target.Evasion.ShouldBe(10);
            arena.Player.GetRequiredComponent<MovementMotor>().Speed.ShouldBe(6);
            arena.Opponent.GetRequiredComponent<MovementMotor>().Speed.ShouldBe(3);
            arena.Player.GetRequiredComponent<ActorStats>().Definition.ShouldNotBeSameAs(
                arena.Opponent.GetRequiredComponent<ActorStats>().Definition);
        }
        finally
        {
            arena.Free();
        }
    }

    /// <summary>A runtime modifier updates speed and combat attributes without mutating the shared definition resource.</summary>
    /// <returns>Completes after the motor consumes the newly published resolved values.</returns>
    [Test]
    public async Task LiveModifiersReachMotorAndCombatProjection()
    {
        var arena = Create();

        try
        {
            var id = arena.Player.CombatantId;
            var profile = Runtime.CombatState.GetStatProfile(id);
            Runtime.CombatState.UpdateStats(id, new(profile.BaseValues,
            [
                new(new(StatType.MovementSpeed), StatOperation.Increased, 0.5m, "boots:test"),
                new(new(StatType.Armor), StatOperation.Flat, 10, "armor:test"),
                new(new(StatType.MaxHealth), StatOperation.Flat, 50, "armor:test")
            ]));
            await Frames(3);
            var state = Runtime.CombatState.Read(id, id).Source;

            arena.Player.GetRequiredComponent<MovementMotor>().Speed.ShouldBe(9);
            state.Armor.ShouldBe(12);
            state.MaxHealth.ShouldBe(150);
            state.Health.ShouldBe(100);
            arena.Player.GetRequiredComponent<ActorStats>().Definition.MovementSpeed.ShouldBe(6);
        }
        finally
        {
            arena.Free();
        }
    }

    /// <summary>Application equip/unequip changes the actual actor motor and combat projection without a second engine calculation.</summary>
    /// <returns>Completes after the motor consumes both published equipment states.</returns>
    [Test]
    public async Task EquipmentChangesReachTheActorMotorAndCombatState()
    {
        var arena = Create();

        try
        {
            var id = arena.Player.CombatantId;
            var item = new ItemFactory(ItemRarityRules.Default, ItemStatRules.Default).Create("test-boots",
                new ItemDefinition("boots", "Boots", ["boots"],
                [
                    new ItemModifier(new StatKey(StatType.MovementSpeed), StatOperation.Increased, 0.5m),
                    new ItemModifier(new StatKey(StatType.Armor), StatOperation.Flat, 10)
                ]), ItemRarity.Common);
            Runtime.CombatState.RegisterOwnedItem(id, item);
            var equipment = new EquipmentService(Runtime.CombatState);

            equipment.EquipItem(id, item.Id, EquipmentSlot.Boots).ShouldBe(EquipmentChangeStatus.Applied);
            await Frames(3);

            arena.Player.GetRequiredComponent<MovementMotor>().Speed.ShouldBe(9);
            Runtime.CombatState.Read(id, id).Source.Armor.ShouldBe(12);
            equipment.UnequipItem(id, EquipmentSlot.Boots).ShouldBe(EquipmentChangeStatus.Applied);
            await Frames(3);

            arena.Player.GetRequiredComponent<MovementMotor>().Speed.ShouldBe(6);
            Runtime.CombatState.Read(id, id).Source.Armor.ShouldBe(2);
            arena.Player.GetRequiredComponent<ActorStats>().Definition.MovementSpeed.ShouldBe(6);
        }
        finally
        {
            arena.Free();
        }
    }

    private GreyboxArena Create()
    {
        var arena = GD.Load<PackedScene>("res://Arena.tscn").Instantiate<GreyboxArena>();
        Runtime.AddChild(arena);
        arena.Player.Controls.HardwareInput = false;
        arena.Opponent.Controls.ThinkingEnabled = false;

        return arena;
    }

    private async Task Frames(int count)
    {
        for (var frame = 0; frame < count; frame++)
        {
            await TestScene.ToSignal(TestScene.GetTree(), SceneTree.SignalName.PhysicsFrame);
        }
    }
}
