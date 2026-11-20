using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Chickensoft.GoDotTest;
using Godot;
using RollForHonor.Application.Combat.Models;
using RollForHonor.Godot.Arena;
using RollForHonor.Godot.Gameplay.Actors;
using RollForHonor.Godot.Gameplay.Combat;
using RollForHonor.Godot.Gameplay.Movement;
using Shouldly;

namespace RollForHonor.Godot.Tests;

/// <summary>Exercises an enemy controller against actual arena physics and the shared combat session.</summary>
public sealed class GodotEnemyTest : TestClass
{
    private GameBootstrap Runtime => TestScene.GetTree().Root.GetChildren()
        .OfType<GameBootstrap>()
        .Single();

    /// <summary>Creates real-engine enemy integration checks.</summary>
    /// <param name="testScene">The host used to await physics frames.</param>
    public GodotEnemyTest(Node testScene) : base(testScene)
    {
    }

    /// <summary>The opponent ignores distant targets, then pursues a visible target using its shared motor.</summary>
    /// <returns>Completes after decisions and movement have run in native physics.</returns>
    [Test]
    public async Task DetectsVisibleTargetsAndPursuesWithinRange()
    {
        var arena = Create(new(-10, 0.05f, 32), new(-40, 0.05f, 32));

        try
        {
            await Frames(10);

            arena.Opponent.Controls.HasVisibleTarget.ShouldBeFalse();
            arena.Opponent.GetRequiredComponent<MovementMotor>().HorizontalSpeed.ShouldBeLessThan(0.01f);

            arena.Opponent.Position = new(-24, 0.05f, 32);
            await Frames(3);
            var before = arena.Opponent.Position.DistanceTo(arena.Player.Position);
            await Frames(45);

            arena.Opponent.Controls.HasVisibleTarget.ShouldBeTrue();
            arena.Opponent.Position.DistanceTo(arena.Player.Position).ShouldBeLessThan(before - 1);
            arena.Opponent.GetRequiredComponent<MovementMotor>().HorizontalSpeed.ShouldBeInRange(2.9f, 3.1f);
        }
        finally
        {
            arena.Free();
        }
    }

    /// <summary>In-range shots stop movement, respect the Application cooldown and damage the player through physical contacts.</summary>
    /// <returns>Completes after two bounded attack opportunities.</returns>
    [Test]
    public async Task FiresThroughCommandsAndApplicationWithIndependentCooldown()
    {
        var arena = Create(new(-10, 0.05f, 32), new(-16, 0.05f, 32));
        var launches = new List<TimeSpan>();
        var committed = 0;
        var combat = arena.Opponent.GetRequiredComponent<ProjectileAttack>();
        combat.LaunchCompleted += result =>
        {
            result.Status.ShouldBe(AttackStartStatus.Started);
            launches.Add(Runtime.GameTime);
        };
        combat.ContactCompleted += (contact, _, _) => committed += contact.Impact?.IsCommitted == true ? 1 : 0;
        Runtime.CombatState.UpdateCombatant(new(arena.Player.CombatantId, 100, 100, 0, 0, [], []));

        try
        {
            await Frames(110);
            var player = Runtime.CombatState.Read(arena.Player.CombatantId, arena.Player.CombatantId).Source;

            launches.Count.ShouldBeInRange(2, 3);
            (launches[1] - launches[0]).ShouldBeGreaterThanOrEqualTo(TimeSpan.FromSeconds(1.5));
            committed.ShouldBeGreaterThan(0);
            player.Health.ShouldBeLessThan(100);
            arena.Opponent.GetRequiredComponent<MovementMotor>().HorizontalSpeed.ShouldBeLessThan(0.01f);
            Runtime.CombatState.ReadForStart(arena.Player.CombatantId, "arena-shot", Guid.NewGuid()).Resources.Single().Amount.ShouldBe(500m);
            arena.GetNode<RichTextLabel>("HUD/Journal/Rows/Text").Text.ShouldContain("Sentinel");
        }
        finally
        {
            arena.Free();
        }
    }

    /// <summary>A physical wall blocks perception and firing; no hidden attack reaches the occluded player.</summary>
    /// <returns>Completes after several decisions behind the real courtyard wall.</returns>
    [Test]
    public async Task WallBlocksDetectionAndAttacks()
    {
        var arena = Create(new(6, 0.05f, 18), new(-5, 0.05f, 18));

        try
        {
            await Frames(45);

            arena.Opponent.Controls.HasVisibleTarget.ShouldBeFalse();
            arena.Opponent.GetRequiredComponent<ProjectileAttack>().LastAttack.ShouldBeNull();
            arena.Opponent.GetRequiredComponent<MovementMotor>().HorizontalSpeed.ShouldBeLessThan(0.01f);
            Runtime.CombatState.Read(arena.Player.CombatantId, arena.Player.CombatantId).Source.Health.ShouldBe(100);
        }
        finally
        {
            arena.Free();
        }
    }

    /// <summary>A player projectile can defeat the dynamic enemy, preventing subsequent AI movement and launches.</summary>
    /// <returns>Completes after a deterministic positive-damage hit and a disabled enemy decision window.</returns>
    [Test]
    public async Task PlayerCanDefeatEnemyThroughTheSameCombatPipeline()
    {
        var arena = Create(new(-10, 0.05f, 32), new(-16, 0.05f, 32));
        arena.Opponent.Controls.ThinkingEnabled = false;
        Runtime.CombatState.UpdateCombatant(new(arena.Opponent.CombatantId, 1, 40, 0, 0, [], []));

        try
        {
            await Frames(3);
            arena.Player.GetRequiredComponent<ProjectileAttack>().RequestAttack(arena.Opponent.AimPoint).Status.ShouldBe(AttackStartStatus.Started);

            for (var frame = 0; frame < 80 && !Runtime.CombatState.Read(arena.Player.CombatantId, arena.Opponent.CombatantId).Target.IsDefeated; frame++)
            {
                await Frames(1);
            }

            Runtime.CombatState.Read(arena.Player.CombatantId, arena.Opponent.CombatantId).Target.IsDefeated.ShouldBeTrue();
            arena.Opponent.Controls.ThinkingEnabled = true;
            await Frames(10);

            arena.Opponent.GetRequiredComponent<ActorState>().IsDefeated.ShouldBeTrue();
            arena.Opponent.GetRequiredComponent<MovementMotor>().HorizontalSpeed.ShouldBeLessThan(0.01f);
            arena.Opponent.GetRequiredComponent<ProjectileAttack>().LastAttack.ShouldBeNull();
            arena.GetNode<Label3D>("Sentinel/Status").Text.ShouldContain("Defeated");
        }
        finally
        {
            arena.Free();
        }
    }

    /// <summary>A defeated player is no longer pursued or attacked.</summary>
    /// <returns>Completes after the controller observes target defeat.</returns>
    [Test]
    public async Task StopsWhenThePlayerIsDefeated()
    {
        var arena = Create(new(-10, 0.05f, 32), new(-24, 0.05f, 32));

        try
        {
            await Frames(25);
            arena.Opponent.GetRequiredComponent<MovementMotor>().HorizontalSpeed.ShouldBeGreaterThan(1);
            Runtime.CombatState.UpdateCombatant(new(arena.Player.CombatantId, 0, 100, 2, 12, [], []));
            await Frames(3);

            arena.Opponent.Controls.HasVisibleTarget.ShouldBeFalse();
            arena.Opponent.GetRequiredComponent<MovementMotor>().HorizontalSpeed.ShouldBeLessThan(0.01f);
            arena.Opponent.GetRequiredComponent<ProjectileAttack>().LastAttack.ShouldBeNull();
        }
        finally
        {
            arena.Free();
        }
    }

    private GreyboxArena Create(Vector3 playerPosition, Vector3 enemyPosition)
    {
        var arena = GD.Load<PackedScene>("res://Arena.tscn").Instantiate<GreyboxArena>();
        Runtime.AddChild(arena);
        arena.Player.Controls.HardwareInput = false;
        arena.Player.Position = playerPosition;
        arena.Opponent.Position = enemyPosition;

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
