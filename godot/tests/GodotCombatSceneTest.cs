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

    /// <summary>Configured vampirism heals only the actual three HP lost by an overkilled enemy.</summary>
    /// <returns>Completes after the actual projectile contacts the enemy and the returned health changes are checked.</returns>
    [Test]
    public async Task VampirismHealsFromActualDamageAndIsExplainedInJournal()
    {
        var scene = CreateScene();
        try
        {
            scene.SourceEditor.GetNode<SpinBox>("HealthRow/Health").Value = 50;
            scene.ApplyDraft().ShouldBeTrue();
            scene.TargetEditor.GetNode<SpinBox>("HealthRow/Health").Value = 3;
            scene.ApplyDraft(true).ShouldBeTrue();
            scene.SourceEditor.GetNode<SpinBox>("ModifierRow/Modifier").Value = 30;
            scene.SourceEditor.GetNode<SpinBox>("FlatDamageRow/FlatDamage").Value = 30;
            scene.SourceEditor.GetNode<CheckBox>("Lifesteal").ButtonPressed = true;
            scene.SourceEditor.GetNode<SpinBox>("LifestealPercentRow/LifestealPercent").Value = 50;
            scene.Fire();
            await WaitForContact(scene);
            var read = Runtime.CombatState.Read(scene.Source.CombatantId, scene.Target.CombatantId);
            read.Source.Health.ShouldBe(52);
            read.Target.Health.ShouldBe(0);
            Balance(scene).ShouldBe(90m);
            var journal = scene.GetNode<RichTextLabel>("HUD/JournalPanel/Rows/Journal").GetParsedText();
            journal.ShouldContain("actual opponent HP lost 3");
            journal.ShouldContain("50 → 52");
        }
        finally { scene.Free(); }
    }

    /// <summary>A manual round ticks both statuses without another d20 or resource payment, even after the source dies.</summary>
    /// <returns>Completes after physical status application and a manually requested periodic round.</returns>
    [Test]
    public async Task ManualRoundTicksCapturedEffectsAfterSourceDeath()
    {
        var scene = CreateScene();
        try
        {
            PreparePeriodicTarget(scene);
            scene.Fire();
            await WaitForContact(scene);
            scene.SourceEditor.GetNode<SpinBox>("HealthRow/Health").Value = 0;
            scene.ApplyDraft().ShouldBeTrue();
            var die = scene.GetNode<Label>("HUD/Die").Text;
            var version = Runtime.CombatState.Read(scene.Source.CombatantId, scene.Target.CombatantId).Version;
            var ticks = scene.NextTick();
            ticks.Count.ShouldBe(2);
            foreach (var tick in ticks)
            {
                tick.IsCommitted.ShouldBeTrue();
                var resolution = ((ResolvedCombat)tick.Outcome).Resolution;
                resolution.DiceTrace.Rolls.Count.ShouldBe(1);
                resolution.Result.Target.AttackRoll.ShouldBeNull();
                tick.Request.StateSnapshot.Source.Health.ShouldBe(100);
            }
            var after = Runtime.CombatState.Read(scene.Source.CombatantId, scene.Target.CombatantId);
            after.Version.ShouldBe(version + 2);
            after.Source.Health.ShouldBe(0);
            scene.GetNode<Label>("HUD/Die").Text.ShouldBe(die);
            Balance(scene).ShouldBe(90m);
        }
        finally { scene.Free(); }
    }

    /// <summary>Automatic rounds use game time, pause safely, reset their deadline on interval/mode changes and share manual execution.</summary>
    /// <returns>Completes after periodic statuses are applied and both scheduling modes are verified.</returns>
    [Test]
    public async Task AutomaticRoundsRespectPauseIntervalAndModeChanges()
    {
        var scene = CreateScene();
        try
        {
            PreparePeriodicTarget(scene);
            scene.Fire();
            await WaitForContact(scene);
            var automatic = scene.GetNode<CheckButton>("HUD/Panel/Rows/TickControls/Auto");
            var interval = scene.GetNode<SpinBox>("HUD/Panel/Rows/TickControls/Interval");
            automatic.ButtonPressed = true;
            scene.GetNode<Button>("HUD/Panel/Rows/NextTick").Disabled.ShouldBeTrue();
            Runtime._Process(2.5);
            scene._Process(0);
            scene.TickRounds.ShouldBe(2);
            var tree = TestScene.GetTree();
            var wasPaused = tree.Paused;
            try
            {
                tree.Paused = true;
                Runtime._Process(10);
                scene._Process(10);
                scene.TickRounds.ShouldBe(2);
            }
            finally { tree.Paused = wasPaused; }
            interval.Value = 0.5;
            Runtime._Process(0.5);
            scene._Process(0);
            scene.TickRounds.ShouldBe(3);
            automatic.ButtonPressed = false;
            Runtime._Process(2);
            scene._Process(0);
            scene.TickRounds.ShouldBe(3);
            scene.GetNode<Button>("HUD/Panel/Rows/NextTick").Disabled.ShouldBeFalse();
            scene.NextTick();
            scene.TickRounds.ShouldBe(4);
        }
        finally { scene.Free(); }
    }

    private static void PreparePeriodicTarget(CombatDemoScene scene)
    {
        scene.TargetEditor.GetNode<SpinBox>("MaxHealthRow/MaxHealth").Value = 500;
        scene.TargetEditor.GetNode<SpinBox>("HealthRow/Health").Value = 500;
        scene.ApplyDraft(true).ShouldBeTrue();
        scene.SourceEditor.GetNode<SpinBox>("ModifierRow/Modifier").Value = 30;
        scene.SourceEditor.GetNode<CheckBox>("Burning").ButtonPressed = true;
        scene.SourceEditor.GetNode<CheckBox>("Bleeding").ButtonPressed = true;
    }

    private GameBootstrap Runtime => TestScene.GetTree().Root.GetChildren().OfType<GameBootstrap>().Single();
    /// <summary>Edits while an arrow is in flight are used by its eventual impact, without charging again.</summary>
    [Test]
    public async Task EditedDefensesAreReadAtContactAndJournalPreservesTheCalculation()
    {
        var scene = CreateScene();
        try
        {
            scene.SourceEditor.GetNode<SpinBox>("ModifierRow/Modifier").Value = 30;
            scene.Fire();
            scene.TargetEditor.GetNode<SpinBox>("HealthRow/Health").Value = 60;
            scene.TargetEditor.GetNode<SpinBox>("MaxHealthRow/MaxHealth").Value = 80;
            scene.TargetEditor.GetNode<SpinBox>("ArmorRow/Armor").Value = 1000;
            scene.TargetEditor.GetNode<SpinBox>("PiercingRow/Piercing").Value = 100;
            scene.ApplyDraft(true).ShouldBeTrue();
            Balance(scene).ShouldBe(90m);
            await WaitForContact(scene);
            Runtime.CombatState.Read(scene.Source.CombatantId, scene.Target.CombatantId).Target.Health.ShouldBe(59);
            var text = scene.GetNode<RichTextLabel>("HUD/JournalPanel/Rows/Journal").GetParsedText();
            text.ShouldContain("armor 1000");
            text.ShouldContain("Complete dice trace");
            text.ShouldContain("60 → 59");
            text.ShouldContain("AttackQuality");
        }
        finally { scene.Free(); }
    }

    /// <summary>The enemy uses its own resources and attack configuration to hit the player through physics.</summary>
    [Test]
    public async Task EnemyAttackUsesEnemyConfigurationAndResources()
    {
        var scene = CreateScene();
        try
        {
            scene.TargetEditor.GetNode<SpinBox>("ModifierRow/Modifier").Value = 30;
            scene.Fire(true).Status.ShouldBe(AttackStartStatus.Started);
            await WaitForContact(scene);
            var read = Runtime.CombatState.Read(scene.Source.CombatantId, scene.Target.CombatantId);
            read.Source.Health.ShouldBeLessThan(100);
            read.Target.Health.ShouldBe(50);
            Balance(scene).ShouldBe(100m);
            Runtime.CombatState.ReadForStart(scene.Target.CombatantId, "demo-arrow", Guid.NewGuid())
                .Resources.Single().Amount.ShouldBe(90m);
            scene.GetNode<RichTextLabel>("HUD/JournalPanel/Rows/Journal").GetParsedText().ShouldContain("Enemy → Player");
        }
        finally { scene.Free(); }
    }

    /// <summary>Catalog selections attach existing captured-source statuses without introducing automatic ticks.</summary>
    [Test]
    public async Task SelectedStatusFixturesAreAppliedAndNamedInTheJournal()
    {
        var scene = CreateScene();
        try
        {
            scene.SourceEditor.GetNode<CheckBox>("Burning").ButtonPressed = true;
            scene.SourceEditor.GetNode<CheckBox>("Bleeding").ButtonPressed = true;
            scene.Fire();
            await WaitForContact(scene);
            var read = Runtime.CombatState.Read(scene.Source.CombatantId, scene.Target.CombatantId);
            read.Target.Effects.Count.ShouldBe(2);
            read.Target.Effects.Select(value => DemoEffectCatalog.Name(value.Definition.Id)).ShouldBe(new[] { "Burning", "Bleeding" });
            foreach (var effect in read.Target.Effects) effect.SourceSnapshot.Id.ShouldBe(scene.Source.CombatantId);
            var journal = scene.GetNode<RichTextLabel>("HUD/JournalPanel/Rows/Journal").GetParsedText();
            journal.ShouldContain("Apply Burning");
            journal.ShouldContain("Apply Bleeding");
            await TestScene.ToSignal(TestScene.GetTree(), SceneTree.SignalName.PhysicsFrame);
            Runtime.CombatState.Read(scene.Source.CombatantId, scene.Target.CombatantId).Version.ShouldBe(read.Version);
        }
        finally { scene.Free(); }
    }

    /// <summary>Invalid HP drafts cannot mutate live state; valid fields and percentage conversions remain editable.</summary>
    [Test]
    public void StatsEditorValidatesDraftsAndBuildsTypedWeaponValues()
    {
        var scene = CreateScene();
        try
        {
            var before = Runtime.CombatState.Read(scene.Source.CombatantId, scene.Target.CombatantId);
            scene.SourceEditor.GetNode<SpinBox>("HealthRow/Health").Value = 200;
            scene.ApplyDraft().ShouldBeFalse();
            Runtime.CombatState.Read(scene.Source.CombatantId, scene.Target.CombatantId).Version.ShouldBe(before.Version);
            scene.SourceEditor.GetNode<SpinBox>("HealthRow/Health").Value = 80;
            scene.SourceEditor.GetNode<SpinBox>("FireRow/Fire").Value = 25;
            scene.SourceEditor.GetNode<SpinBox>("SlashingRow/Slashing").Value = -20;
            scene.SourceEditor.GetNode<SpinBox>("DiceCountRow/DiceCount").Value = 2;
            scene.SourceEditor.GetNode<SpinBox>("DiceSidesRow/DiceSides").Value = 8;
            scene.SourceEditor.GetNode<SpinBox>("FlatDamageRow/FlatDamage").Value = 4;
            scene.ApplyDraft().ShouldBeTrue();
            var live = Runtime.CombatState.Read(scene.Source.CombatantId, scene.Target.CombatantId).Source;
            live.Health.ShouldBe(80);
            live.Resistances.Single(value => value.Type.ToString() == "Fire").Value.ShouldBe(0.25m);
            live.Resistances.Single(value => value.Type.ToString() == "Slashing").Value.ShouldBe(-0.2m);
            var damage = scene.SourceEditor.BuildAttack().Damage.Single();
            damage.Dice.Count.ShouldBe(2);
            damage.Dice.Sides.ShouldBe(8);
            damage.FlatDamage.ShouldBe(4);
        }
        finally { scene.Free(); }
    }

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
