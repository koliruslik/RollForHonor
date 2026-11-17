using RollForHonor.Application.Combat.Models;
using RollForHonor.Application.Combat.Ports;
using RollForHonor.Application.Combat.Services;
using RollForHonor.Domain.Combat.Damage.Models;
using RollForHonor.Domain.Combat.Damage.Services;
using RollForHonor.Domain.Combat.Effects.Models;
using RollForHonor.Domain.Combat.Resolution.Models;
using RollForHonor.Domain.Combat.Resolution.Services;
using RollForHonor.Domain.Combat.Resolution.Services.Steps;
using RollForHonor.Domain.Combat.State.Models;
using RollForHonor.Domain.Dice.Models;
using RollForHonor.Domain.Dice.Services;
using RollForHonor.Domain.Resources.Models;
using RollForHonor.Domain.Tests.Diagnostics;
using RollForHonor.Infrastructure.Combat;
using Xunit.Abstractions;

namespace RollForHonor.Application.Tests.Combat;

/// <summary>Verifies periodic use-case capture, fresh state, admission and atomic publication boundaries.</summary>
public sealed class PeriodicEffectServiceTests : CombatDiagnosticTestBase
{
    /// <summary>Creates periodic use-case tests with opt-in calculation diagnostics.</summary>
    /// <param name="output">The current xUnit output.</param>
    public PeriodicEffectServiceTests(ITestOutputHelper output) : base(output) { }

    /// <summary>Each tick rerolls independently against fresh defenses and uses its captured source without a live source entry.</summary>
    [Fact]
    public void TicksUseCapturedSourceFreshRecipientAndNoLaunchResources()
    {
        var rig = new Rig();
        var first = Tick(rig);
        Assert.True(first.IsCommitted);
        Assert.Same(rig.Source, first.Request!.StateSnapshot.Source);
        Assert.Null(first.Request.Attack);
        var after = rig.Read().Target;
        rig.State.UpdateCombatant(new(after.Id, after.Health, after.MaxHealth, 0, 0,
            [new(DamageType.Fire, 0.5m)], after.Effects));
        var second = Tick(rig);
        Assert.NotEqual(first.ResolutionId, second.ResolutionId);
        Assert.Equal(44, rig.Read().Target.Health);
        Assert.Equal(2, rig.DiceCreated);
        var resolution = Assert.IsType<ResolvedCombat>(second.Outcome).Resolution;
        Assert.Null(resolution.Result.Target.AttackRoll);
        Assert.Single(resolution.DiceTrace.Rolls);
        Assert.Equal(100, rig.State.ReadForStart(rig.Target.Id, "tick", Guid.NewGuid()).Resources.Single().Amount);
        Assert.Equal(TimeSpan.Zero, rig.State.ReadForStart(rig.Target.Id, "tick", Guid.NewGuid()).ReadyAt);
    }

    /// <summary>Dead live sources and self-target statuses do not replace the captured calculation source.</summary>
    /// <param name="self">Whether source and recipient share an identity.</param>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void SourceDeathOrSelfTargetDoesNotInvalidateCapturedTick(bool self)
    {
        var rig = new Rig(self);
        if (!self) rig.State.AddCombatant(new(rig.Source.Id, 0, 100, 0, 0, [], []));
        var result = Tick(rig);
        Assert.True(result.IsCommitted);
        Assert.Equal(46, rig.Read().Target.Health);
        Assert.Equal(50, result.Request!.StateSnapshot.Source.Health);
    }

    /// <summary>Invalid recipient/effect state is rejected before dice or publication.</summary>
    /// <param name="kind">Missing, replaced, uncaptured, nonperiodic or defeated state.</param>
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    public void IneligibleTicksDoNotRollOrCommit(int kind)
    {
        var rig = new Rig();
        CombatEffectSnapshot[] effects = kind switch
        {
            0 => [],
            1 => [new(new(Guid.NewGuid()), rig.Effect.Definition, rig.Source)],
            2 => [new(rig.Effect.Id, rig.Effect.Definition)],
            3 => [new(rig.Effect.Id, new NonPeriodic(), rig.Source)],
            _ => [rig.Effect]
        };
        rig.State.UpdateCombatant(new(rig.Target.Id, kind == 4 ? 0 : 50, 100, 0, 0, [], effects));
        var version = rig.Read().Version;
        var result = Tick(rig);
        Assert.IsType<RejectedCombat>(result.Outcome);
        Assert.Null(result.Request);
        Assert.Null(result.CommitStatus);
        Assert.Equal(0, rig.DiceCreated);
        Assert.Equal(version, rig.Read().Version);
    }

    /// <summary>A conflict after calculation publishes no damage and does not implicitly reroll or retry.</summary>
    [Fact]
    public void VersionConflictDoesNotPublishTheTick()
    {
        var rig = new Rig();
        var state = new CommitHook(rig.State, () => rig.State.UpdateCombatant(rig.Read().Target));
        var service = rig.Service(state);
        var result = service.Tick(rig.Target.Id, rig.Effect.Id);
        LogOutcome(result.Outcome);
        Assert.Equal(CombatCommitStatus.VersionConflict, result.CommitStatus);
        Assert.Equal(50, rig.Read().Target.Health);
        Assert.Equal(1, rig.DiceCreated);
        Assert.Equal(1, state.CommitCalls);
    }

    /// <summary>A used dice stream is rejected before any new roll or target mutation.</summary>
    [Fact]
    public void UsedDiceTraceCannotLeakBetweenTicks()
    {
        var rig = new Rig();
        var dice = new OnesDice();
        dice.Roll(new(1, 6));
        var service = new PeriodicEffectService(rig.State, Pipeline(), () => dice);
        var version = rig.Read().Version;
        Assert.Throws<InvalidOperationException>(() => service.Tick(rig.Target.Id, rig.Effect.Id));
        Assert.Equal(version, rig.Read().Version);
        Assert.Single(dice.Trace.Rolls);
    }

    private PeriodicTickResult Tick(Rig rig)
    {
        var result = rig.Service(rig.State).Tick(rig.Target.Id, rig.Effect.Id);
        LogOutcome(result.Outcome);
        return result;
    }
    private static CombatPipeline Pipeline() => new([new RawDamageStep(new RawDamageResolver()),
        new DefenseStep(new DefenseResolver(new HitMagnitudeArmorFormula()))]);
    private sealed class Rig
    {
        internal CombatantSnapshot Source { get; }
        internal CombatantSnapshot Target { get; }
        internal CombatEffectSnapshot Effect { get; }
        internal InMemoryCombatState State { get; } = new();
        internal int DiceCreated { get; private set; }
        internal Rig(bool self = false)
        {
            Source = new(new(Guid.NewGuid()), 50, 100, 0, 0, [], []);
            Effect = new(new(Guid.NewGuid()), new PeriodicDamageEffectDefinition(new(Guid.NewGuid()), [new(new(1, 2), 3, DamageType.Fire)]), Source);
            Target = new(self ? Source.Id : new(Guid.NewGuid()), 50, 100, 0, 0, [], [Effect]);
            State.AddCombatant(Target, [new(new("mana"), 100)]);
        }
        internal CombatStateRead Read() => State.Read(Target.Id, Target.Id);
        internal PeriodicEffectService Service(ICombatStateAdapter state) => new(state, Pipeline(), () => { DiceCreated++; return new OnesDice(); });
    }
    private sealed class CommitHook(InMemoryCombatState state, Action before) : ICombatStateAdapter
    {
        internal int CommitCalls { get; private set; }
        /// <inheritdoc />
        public CombatStateRead Read(CombatantId sourceId, CombatantId targetId) => state.Read(sourceId, targetId);
        /// <inheritdoc />
        public CombatCommitStatus TryCommit(CombatResolution resolution)
        {
            CommitCalls++;
            before();
            return state.TryCommit(resolution);
        }
    }
    private sealed record NonPeriodic : CombatEffectDefinition
    {
        internal NonPeriodic() : base(new EffectDefinitionId(Guid.NewGuid())) { }
    }
    private sealed class OnesDice : IDiceRoller
    {
        private readonly List<DiceRoll> _rolls = [];
        /// <inheritdoc />
        public DiceTrace Trace => new(_rolls);
        /// <inheritdoc />
        public DiceRoll Roll(DiceFormula formula)
        {
            var roll = new DiceRoll(formula, Enumerable.Repeat(1, formula.Count).ToArray(), formula.Count);
            _rolls.Add(roll);
            return roll;
        }
    }
}
