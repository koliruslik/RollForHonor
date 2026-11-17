using System;
using System.Linq;
using System.Threading.Tasks;
using Chickensoft.GoDotTest;
using Godot;
using RollForHonor.Application.Combat.Models;
using RollForHonor.Godot.CombatDemo;
using Shouldly;

namespace RollForHonor.Godot.Tests;

/// <summary>Verifies real hurtbox contacts, per-target admission and melee window termination through the complete runtime.</summary>
public sealed class GodotMeleeSceneTest : TestClass
{
    /// <summary>Creates melee integration tests in the actual Godot test host.</summary>
    /// <param name="testScene">The scene used to await physics and locate bootstrap.</param>
    public GodotMeleeSceneTest(Node testScene) : base(testScene) { }

    /// <summary>One window detects initially overlapping targets and cannot repay costs or hit a target twice on reentry.</summary>
    /// <returns>Completes after actual overlap, separation and reentry frames.</returns>
    [Test]
    public async Task OneWindowHitsMultipleTargetsOnceWithoutRecharging()
    {
        var scene = CreateScene();
        try
        {
            scene.Target.GlobalPosition = scene.Source.GlobalPosition + Vector3.Right * 1.5f;
            var extra = new DemoCombatant { CollisionLayer = 2, CollisionMask = 0 };
            var hurtbox = new DemoHurtbox { CollisionLayer = 8, CollisionMask = 0, Monitoring = false };
            hurtbox.AddChild(new CollisionShape3D { Shape = new BoxShape3D { Size = Vector3.One } });
            extra.AddChild(hurtbox);
            scene.AddChild(extra);
            extra.GlobalPosition = scene.Source.GlobalPosition + new Vector3(1.5f, 0, 0.8f);
            Runtime.CombatState.AddCombatant(new(extra.CombatantId, 100, 100, 0, 0, [], []));
            var start = scene.Fire();
            scene.Fire().Status.ShouldBe(AttackStartStatus.OnCooldown);
            for (var frame = 0; frame < 20 && scene.CommittedImpacts < 2; frame++) await PhysicsFrame();
            scene.CommittedImpacts.ShouldBe(2);
            start.Attack.IsActive.ShouldBeTrue();
            Runtime.CombatState.Read(scene.Source.CombatantId, extra.CombatantId).Target.Health.ShouldBeLessThan(100);
            var first = Runtime.CombatState.Read(scene.Source.CombatantId, scene.Target.CombatantId).Target.Health;
            first.ShouldBeLessThan(50);
            scene.Target.Position += Vector3.Forward * 10;
            for (var frame = 0; frame < 4; frame++) await PhysicsFrame();
            scene.Target.Position -= Vector3.Forward * 10;
            for (var frame = 0; frame < 4; frame++) await PhysicsFrame();
            scene.CommittedImpacts.ShouldBe(2);
            start.Attack.IsActive.ShouldBeTrue();
            scene.LastContact.Status.ShouldBe(AttackContactStatus.TargetAlreadyContacted);
            Runtime.CombatState.Read(scene.Source.CombatantId, scene.Target.CombatantId).Target.Health.ShouldBe(first);
            Runtime.CombatState.ReadForStart(scene.Source.CombatantId, "demo-melee", Guid.NewGuid()).Resources.Single().Amount.ShouldBe(90m);
            scene.Source.GetChildren().OfType<DemoMeleeHitbox>().Count().ShouldBe(1);
        }
        finally { scene.Free(); }
    }

    /// <summary>An out-of-range attack spends once and ends its window without any impact or refund.</summary>
    /// <returns>Completes when the real window has ended and its node has been removed.</returns>
    [Test]
    public async Task EmptyWindowClosesAndCannotAcceptLaterContacts()
    {
        var scene = CreateScene();
        try
        {
            var start = scene.Fire();
            var version = Runtime.CombatState.Read(scene.Source.CombatantId, scene.Target.CombatantId).Version;
            for (var frame = 0; frame < 90 && start.Attack.IsActive; frame++) await PhysicsFrame();
            await PhysicsFrame();
            start.Attack.EndReason.ShouldBe(AttackEndReason.AnimationFinished);
            scene.Source.GetChildren().OfType<DemoMeleeHitbox>().ShouldBeEmpty();
            scene.Target.GlobalPosition = scene.Source.GlobalPosition + Vector3.Right * 1.5f;
            for (var frame = 0; frame < 3; frame++) await PhysicsFrame();
            scene.CommittedImpacts.ShouldBe(0);
            scene.LastContact.ShouldBeNull();
            Runtime.CombatState.Read(scene.Source.CombatantId, scene.Target.CombatantId).Version.ShouldBe(version);
            Runtime.CombatState.ReadForStart(scene.Source.CombatantId, "demo-melee", Guid.NewGuid()).Resources.Single().Amount.ShouldBe(90m);
        }
        finally { scene.Free(); }
    }

    /// <summary>A physical actor body is insufficient for melee contact when its separate hurtbox is disabled.</summary>
    /// <returns>Completes after the melee window closes without changing the target.</returns>
    [Test]
    public async Task DisabledHurtboxDoesNotReceiveDamage()
    {
        var scene = CreateScene();
        try
        {
            scene.Target.GetNode<DemoHurtbox>("Hurtbox").Monitorable = false;
            scene.Target.GlobalPosition = scene.Source.GlobalPosition + Vector3.Right * 1.5f;
            var start = scene.Fire();
            for (var frame = 0; frame < 90 && start.Attack.IsActive; frame++) await PhysicsFrame();
            scene.CommittedImpacts.ShouldBe(0);
            Runtime.CombatState.Read(scene.Source.CombatantId, scene.Target.CombatantId).Target.Health.ShouldBe(50);
        }
        finally { scene.Free(); }
    }

    /// <summary>The melee strategy reuses typed damage, statuses and vampirism without another combat calculation implementation.</summary>
    /// <returns>Completes after the enemy hurtbox is physically contacted.</returns>
    [Test]
    public async Task MeleeReusesStatusAndVampirismRules()
    {
        var scene = CreateScene();
        try
        {
            scene.SourceEditor.GetNode<SpinBox>("HealthRow/Health").Value = 50;
            scene.ApplyDraft().ShouldBeTrue();
            scene.SourceEditor.GetNode<CheckBox>("Burning").ButtonPressed = true;
            scene.SourceEditor.GetNode<CheckBox>("Bleeding").ButtonPressed = true;
            scene.SourceEditor.GetNode<CheckBox>("Lifesteal").ButtonPressed = true;
            scene.SourceEditor.GetNode<SpinBox>("LifestealPercentRow/LifestealPercent").Value = 50;
            scene.Target.GlobalPosition = scene.Source.GlobalPosition + Vector3.Right * 1.5f;
            scene.Fire();
            for (var frame = 0; frame < 20 && scene.LastContact is null; frame++) await PhysicsFrame();
            scene.LastContact.ShouldNotBeNull();
            var state = Runtime.CombatState.Read(scene.Source.CombatantId, scene.Target.CombatantId);
            state.Target.Effects.Count.ShouldBe(2);
            state.Source.Health.ShouldBe(50 + (int)Math.Ceiling((50 - state.Target.Health) * 0.5m));
        }
        finally { scene.Free(); }
    }

    private GameBootstrap Runtime => TestScene.GetTree().Root.GetChildren().OfType<GameBootstrap>().Single();
    private CombatDemoScene CreateScene()
    {
        var scene = GD.Load<PackedScene>("res://Game.tscn").Instantiate<CombatDemoScene>();
        Runtime.AddChild(scene);
        scene.GetNode<OptionButton>("HUD/Panel/Rows/DeliveryControls/Delivery").Select(1);
        scene.SourceEditor.GetNode<SpinBox>("ModifierRow/Modifier").Value = 30;
        return scene;
    }
    private async Task PhysicsFrame() => await TestScene.ToSignal(TestScene.GetTree(), SceneTree.SignalName.PhysicsFrame);
}
