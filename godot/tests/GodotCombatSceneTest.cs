using System;
using System.Linq;
using System.Threading.Tasks;
using Chickensoft.GoDotTest;
using Godot;
using RollForHonor.Application.Combat.Models;
using RollForHonor.Domain.Combat.Resolution.Models;
using RollForHonor.Domain.Combat.State.Changes;
using RollForHonor.Domain.Resources.Models;
using RollForHonor.Godot.CombatDemo;
using Shouldly;

namespace RollForHonor.Godot.Tests;

/// <summary>Exercises the actual packed scene and physics callbacks without fabricating contacts.</summary>
public sealed class GodotCombatSceneTest : TestClass
{
    /// <summary>Creates scene integration tests in the running engine.</summary>
    /// <param name="testScene">The test host used to locate the runtime and await engine frames.</param>
    public GodotCombatSceneTest(Node testScene) : base(testScene) { }

    /// <summary>A launch spends once, refuses a cooldown replay and commits only after a real body contact.</summary>
    [Test]
    public async Task ProjectileContactCommitsThroughApplication()
    {
        var scene = CreateScene();
        try
        {
            var initial = Runtime.CombatState.Read(scene.Source.CombatantId, scene.Target.CombatantId);
            var start = scene.Fire();
            start.Status.ShouldBe(AttackStartStatus.Started);
            scene.Fire().Status.ShouldBe(AttackStartStatus.OnCooldown);
            scene.LastContact.ShouldBeNull();
            Runtime.CombatState.Read(scene.Source.CombatantId, scene.Target.CombatantId).Target.Health.ShouldBe(initial.Target.Health);
            Balance(scene).ShouldBe(90m);
            await WaitForContact(scene);
            scene.LastContact.Impact.IsCommitted.ShouldBeTrue();
            scene.CommittedImpacts.ShouldBe(1);
            start.Attack.EndReason.ShouldBe(AttackEndReason.CombatantContact);
            var resolution = ((ResolvedCombat)scene.LastContact.Impact.Outcome).Resolution;
            var health = resolution.Changes.OfType<HealthChanged>().SingleOrDefault();
            Runtime.CombatState.Read(scene.Source.CombatantId, scene.Target.CombatantId).Target.Health
                .ShouldBe(health?.CurrentHealth ?? initial.Target.Health);
            scene.GetNode<Label>("HUD/Die").Text.ShouldBe($"d20\n{resolution.Result.Target.AttackRoll.Roll.Results.Single()}");
            Balance(scene).ShouldBe(90m);
            await TestScene.ToSignal(TestScene.GetTree(), SceneTree.SignalName.PhysicsFrame);
            scene.CommittedImpacts.ShouldBe(1);
            scene.GetChildren().OfType<DemoProjectile>().ShouldBeEmpty();
        }
        finally { scene.Free(); }
    }

    /// <summary>A real obstacle contact ends the projectile without combat dice or target state changes.</summary>
    [Test]
    public async Task ObstacleStopsProjectileWithoutAnImpact()
    {
        var scene = CreateScene();
        try
        {
            var obstacle = new StaticBody3D { CollisionLayer = 4, CollisionMask = 0, Position = new(0, 0.9f, 0) };
            obstacle.AddChild(new CollisionShape3D { Shape = new BoxShape3D { Size = new(1, 2, 2) } });
            scene.AddChild(obstacle);
            var start = scene.Fire();
            var version = Runtime.CombatState.Read(scene.Source.CombatantId, scene.Target.CombatantId).Version;
            await WaitForContact(scene);
            scene.LastContact.Impact.ShouldBeNull();
            start.Attack.EndReason.ShouldBe(AttackEndReason.EnvironmentContact);
            scene.CommittedImpacts.ShouldBe(0);
            Runtime.CombatState.Read(scene.Source.CombatantId, scene.Target.CombatantId).Version.ShouldBe(version);
            Balance(scene).ShouldBe(90m);
        }
        finally { scene.Free(); }
    }

    /// <summary>An arrow that misses expires and is removed without creating a combat transaction.</summary>
    [Test]
    public async Task MissedProjectileExpiresWithoutAnImpact()
    {
        var scene = CreateScene();
        try
        {
            var start = scene.Fire();
            scene.Target.Position += Vector3.Back * 4;
            var version = Runtime.CombatState.Read(scene.Source.CombatantId, scene.Target.CombatantId).Version;
            for (var frame = 0; frame < 300 && GodotObject.IsInstanceValid(scene.GetChildren().OfType<DemoProjectile>().FirstOrDefault()); frame++)
                await TestScene.ToSignal(TestScene.GetTree(), SceneTree.SignalName.PhysicsFrame);
            start.Attack.EndReason.ShouldBe(AttackEndReason.LifetimeExpired);
            scene.LastContact.ShouldBeNull();
            scene.GetChildren().OfType<DemoProjectile>().ShouldBeEmpty();
            Runtime.CombatState.Read(scene.Source.CombatantId, scene.Target.CombatantId).Version.ShouldBe(version);
            Balance(scene).ShouldBe(90m);
        }
        finally { scene.Free(); }
    }

    private GameBootstrap Runtime => TestScene.GetTree().Root.GetChildren().OfType<GameBootstrap>().Single();
    private CombatDemoScene CreateScene()
    {
        var scene = GD.Load<PackedScene>("res://Game.tscn").Instantiate<CombatDemoScene>();
        Runtime.AddChild(scene);
        return scene;
    }
    private decimal Balance(CombatDemoScene scene) => Runtime.CombatState
        .ReadForStart(scene.Source.CombatantId, "demo-arrow", Guid.NewGuid()).Resources
        .Single(value => value.Resource == new ResourceId("mana")).Amount;
    private async Task WaitForContact(CombatDemoScene scene)
    {
        for (var frame = 0; frame < 180 && scene.LastContact is null; frame++)
            await TestScene.ToSignal(TestScene.GetTree(), SceneTree.SignalName.PhysicsFrame);
        scene.LastContact.ShouldNotBeNull("the engine must report a physical contact");
    }
}
