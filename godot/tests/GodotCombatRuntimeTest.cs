using System;
using System.Linq;
using Chickensoft.GoDotTest;
using Godot;
using Microsoft.Extensions.Logging;
using RollForHonor.Application.Combat.Models;
using RollForHonor.Domain.Combat.Attacks.Models;
using RollForHonor.Domain.Combat.Damage.Models;
using RollForHonor.Domain.Combat.Resolution.Models;
using RollForHonor.Domain.Combat.State.Changes;
using RollForHonor.Domain.Combat.State.Models;
using RollForHonor.Domain.Resources.Models;
using RollForHonor.Godot.Diagnostics;
using Shouldly;

namespace RollForHonor.Godot.Tests;

/// <summary>Checks the real bootstrap, runtime adapters and Godot clock/output inside a running engine.</summary>
public sealed class GodotCombatRuntimeTest : TestClass
{
    /// <summary>Creates runtime tests in the scene supplied by GoDotTest.</summary>
    /// <param name="testScene">The active test scene used to locate the real composition root.</param>
    public GodotCombatRuntimeTest(Node testScene) : base(testScene) { }

    /// <summary>Exercises shared launch/impact state, runtime randomness, receipts and once-only resource cost.</summary>
    [Test]
    public void BootstrapLaunchAndImpactsUseTheSameSession()
    {
        var bootstrap = Bootstrap;
        var source = Character();
        var target = Character();
        var mana = new ResourceId("mana");
        bootstrap.CombatState.AddCombatant(source, new[] { new ResourceAmount(mana, 30) });
        bootstrap.CombatState.AddCombatant(target);
        var definition = Definition(new[] { new ResourceAmount(mana, 10) });
        var requestId = Guid.NewGuid();
        var started = bootstrap.Attacks.Start(requestId, source.Id, definition);
        started.Status.ShouldBe(AttackStartStatus.Started);
        var first = started.Attack.Resolve(target.Id);
        var second = started.Attack.Resolve(target.Id);
        first.IsCommitted.ShouldBeTrue();
        second.IsCommitted.ShouldBeTrue();
        first.ResolutionId.ShouldNotBe(second.ResolutionId);
        var firstResolution = ((ResolvedCombat)first.Outcome).Resolution;
        var secondResolution = ((ResolvedCombat)second.Outcome).Resolution;
        firstResolution.DiceTrace.Rolls.Count.ShouldBe(2);
        secondResolution.DiceTrace.Rolls.Count.ShouldBe(2);
        var health = secondResolution.Changes.OfType<HealthChanged>().Single();
        bootstrap.CombatState.Read(source.Id, target.Id).Target.Health.ShouldBe(health.CurrentHealth);
        bootstrap.CombatState.ReadForStart(source.Id, definition.Key, Guid.NewGuid()).Resources
            .Single(value => value.Resource == mana).Amount.ShouldBe(20m);
        var replay = bootstrap.Attacks.Start(requestId, source.Id, definition);
        replay.Status.ShouldBe(AttackStartStatus.AlreadyStarted);
        replay.AttackId.ShouldBe(started.AttackId);
        replay.Attack.ShouldBeNull();
    }

    /// <summary>Expiry uses the same accumulated game clock as launch cooldowns, without sleeping or wall-clock access.</summary>
    [Test]
    public void TimedAttackUsesBootstrapGameTime()
    {
        var bootstrap = Bootstrap;
        var source = Character();
        var target = Character();
        bootstrap.CombatState.AddCombatant(source);
        bootstrap.CombatState.AddCombatant(target);
        var started = bootstrap.Attacks.Start(Guid.NewGuid(), source.Id,
            Definition(Array.Empty<ResourceAmount>(), new(maxLifetime: TimeSpan.FromSeconds(1))));
        started.Status.ShouldBe(AttackStartStatus.Started);
        var version = bootstrap.CombatState.Read(source.Id, target.Id).Version;
        bootstrap._Process(0.5);
        started.Attack.UpdateLifetime().ShouldBeTrue();
        bootstrap._Process(0.5);
        started.Attack.UpdateLifetime().ShouldBeFalse();
        started.Attack.EndReason.ShouldBe(AttackEndReason.LifetimeExpired);
        started.Attack.ResolveContact(target.Id).Impact.ShouldBeNull();
        bootstrap.CombatState.Read(source.Id, target.Id).Version.ShouldBe(version);
    }

    /// <summary>Paused scene trees do not advance the game clock even if a process callback is invoked explicitly.</summary>
    [Test]
    public void PausedTreeDoesNotAdvanceGameTime()
    {
        var tree = TestScene.GetTree();
        var previous = tree.Paused;
        var bootstrap = Bootstrap;
        var before = bootstrap.GameTime;
        try
        {
            tree.Paused = true;
            bootstrap._Process(1);
            bootstrap.GameTime.ShouldBe(before);
        }
        finally { tree.Paused = previous; }
    }

    /// <summary>The clock consumes supplied engine delta once instead of multiplying time scale a second time.</summary>
    [Test]
    public void SuppliedScaledDeltaIsNotScaledTwice()
    {
        var previous = Engine.TimeScale;
        var bootstrap = Bootstrap;
        var before = bootstrap.GameTime;
        try
        {
            Engine.TimeScale = 3;
            bootstrap._Process(0.25);
            bootstrap.GameTime.ShouldBe(before + TimeSpan.FromSeconds(0.25));
        }
        finally { Engine.TimeScale = previous; }
    }

    /// <summary>The standard logger adapter can actually write to Godot output in the engine process.</summary>
    [Test]
    public void RuntimeDiagnosticLoggerUsesGodotOutput()
    {
        ILogger logger = new GodotCombatLogger();
        logger.IsEnabled(LogLevel.Information).ShouldBeTrue();
        logger.IsEnabled(LogLevel.Debug).ShouldBeFalse();
        logger.IsEnabled(LogLevel.None).ShouldBeFalse();
        logger.LogInformation("Runtime combat diagnostic output is connected.");
    }

    private GameBootstrap Bootstrap => TestScene.GetTree().Root.GetChildren().OfType<GameBootstrap>().Single();
    private static CombatantSnapshot Character() => new(new(Guid.NewGuid()), 100, 100, 0, 0, [], []);
    private static AttackDefinition Definition(System.Collections.Generic.IReadOnlyList<ResourceAmount> costs,
        AttackExecutionRules rules = null) => new("runtime-smoke",
        new(new(1, 20), 30, NaturalOutcomeShiftRules.Standard, [new(new(1, 6), 3, DamageType.Slashing)], [], AttackTags.Projectile),
        TimeSpan.FromSeconds(2), costs, rules);
}
