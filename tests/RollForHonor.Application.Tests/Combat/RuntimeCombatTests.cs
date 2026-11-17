using RollForHonor.Application.Combat.Models;
using RollForHonor.Application.Combat.Services;
using RollForHonor.Domain.Combat.Attacks.Models;
using RollForHonor.Domain.Combat.Damage.Configuration;
using RollForHonor.Domain.Combat.Damage.Models;
using RollForHonor.Domain.Combat.Damage.Services;
using RollForHonor.Domain.Combat.Effects.Models;
using RollForHonor.Domain.Combat.Resolution.Models;
using RollForHonor.Domain.Combat.Resolution.Services;
using RollForHonor.Domain.Combat.Resolution.Services.Steps;
using RollForHonor.Domain.Combat.State.Changes;
using RollForHonor.Domain.Combat.State.Models;
using RollForHonor.Domain.Combat.State.Services;
using RollForHonor.Domain.Dice.Models;
using RollForHonor.Domain.Resources.Models;
using RollForHonor.Domain.Resources.Services;
using RollForHonor.Domain.Tests.Diagnostics;
using RollForHonor.Infrastructure.Combat;
using RollForHonor.Infrastructure.Dice;
using Xunit.Abstractions;

namespace RollForHonor.Application.Tests.Combat;

/// <summary>Exercises runtime adapters with real Application/Domain rules instead of test-state fault hooks.</summary>
public sealed class RuntimeCombatTests : CombatDiagnosticTestBase
{
    /// <summary>Creates runtime contract tests with opt-in outcome output.</summary>
    /// <param name="output">The current xUnit output.</param>
    public RuntimeCombatTests(ITestOutputHelper output) : base(output) { }

    /// <summary>One runtime launch charges once while multiple contacts use fresh dice and committed state.</summary>
    [Fact]
    public void LaunchAndImpactsShareStateResourcesAndVersions()
    {
        var rig = new Rig();
        var attack = rig.Start();
        var first = Resolve(attack, rig.Target.Id);
        var second = Resolve(attack, rig.Target.Id);
        var last = ((ResolvedCombat)second.Outcome).Resolution;
        Assert.NotEqual(first.ResolutionId, second.ResolutionId);
        Assert.True(first.IsCommitted);
        Assert.True(second.IsCommitted);
        Assert.Equal(80, rig.ReadStart().Resources.Single(value => value.Resource == Rig.Mana).Amount);
        Assert.Equal(TimeSpan.FromSeconds(2), rig.ReadStart().ReadyAt);
        Assert.Equal(Assert.Single(last.Changes.OfType<HealthChanged>()).CurrentHealth, rig.Read().Target.Health);
        Assert.Equal(2, ((ResolvedCombat)first.Outcome).Resolution.DiceTrace.Rolls.Count);
        Assert.Equal(2, last.DiceTrace.Rolls.Count);
        Assert.Equal(last.BasedOnStateVersion + 1, rig.Read().Version);
    }

    /// <summary>Receipts preserve the original attack and reject command identity reuse for another key.</summary>
    [Fact]
    public void LaunchReceiptPreventsSecondChargeAndDifferentCommandReuse()
    {
        var rig = new Rig();
        var request = Guid.NewGuid();
        var first = rig.Starts.Start(request, rig.Source.Id, rig.Definition);
        var version = rig.Read().Version;
        var replay = rig.Starts.Start(request, rig.Source.Id, rig.Definition);
        Assert.Equal(AttackStartStatus.AlreadyStarted, replay.Status);
        Assert.Equal(first.AttackId, replay.AttackId);
        Assert.Null(replay.Attack);
        Assert.Equal(version, rig.Read().Version);
        Assert.Equal(80, rig.ReadStart().Resources.Single(value => value.Resource == Rig.Mana).Amount);
        Assert.Throws<ArgumentException>(() => rig.State.ReadForStart(rig.Source.Id, "another-key", request));
        Assert.Equal(version, rig.Read().Version);
    }

    /// <summary>A stale proposal is rejected even while the resource balance remains sufficient.</summary>
    [Fact]
    public void StaleLaunchPublishesNoCostsCooldownOrReceipt()
    {
        var rig = new Rig();
        var proposal = StartProposal(rig, [new(Rig.Mana, 20)]);
        rig.State.AddCombatant(Character());
        var version = rig.Read().Version;
        Assert.Equal(CombatCommitStatus.VersionConflict, rig.State.TryStart(proposal));
        Assert.Equal(100, rig.ReadStart().Resources.Single(value => value.Resource == Rig.Mana).Amount);
        Assert.Equal(TimeSpan.Zero, rig.ReadStart().ReadyAt);
        Assert.Null(rig.State.ReadForStart(rig.Source.Id, proposal.DefinitionKey, proposal.RequestId).StartedAttackId);
        Assert.Equal(version, rig.Read().Version);
    }

    /// <summary>Failure on a later resource cannot leave an earlier staged debit or cooldown behind.</summary>
    [Fact]
    public void UnaffordableSecondResourceRollsBackTheWholeLaunch()
    {
        var rig = new Rig();
        var proposal = StartProposal(rig, [new(Rig.Mana, 10), new(new("stamina"), 100)]);
        var before = rig.Read().Version;
        Assert.Throws<InvalidOperationException>(() => rig.State.TryStart(proposal));
        Assert.Equal(before, rig.Read().Version);
        Assert.Equal(100, rig.ReadStart().Resources.Single(value => value.Resource == Rig.Mana).Amount);
        Assert.Equal(TimeSpan.Zero, rig.ReadStart().ReadyAt);
        Assert.Null(rig.State.ReadForStart(rig.Source.Id, proposal.DefinitionKey, proposal.RequestId).StartedAttackId);
    }

    /// <summary>Only one concurrent proposal based on a shared session version can publish.</summary>
    [Fact]
    public void ConcurrentLaunchesAtOneVersionHaveOneWinner()
    {
        var rig = new Rig();
        var first = StartProposal(rig, [new(Rig.Mana, 10)]);
        var second = StartProposal(rig, [new(Rig.Mana, 10)]);
        var statuses = new CombatCommitStatus[2];
        Parallel.Invoke(() => statuses[0] = rig.State.TryStart(first), () => statuses[1] = rig.State.TryStart(second));
        Assert.Single(statuses, status => status == CombatCommitStatus.Applied);
        Assert.Single(statuses, status => status == CombatCommitStatus.VersionConflict);
        Assert.Equal(90, rig.ReadStart().Resources.Single(value => value.Resource == Rig.Mana).Amount);
    }

    /// <summary>Invalid later changes cannot partially publish actors, advance versions or consume a receipt.</summary>
    /// <param name="failure">Stale health, excessive health, false defeat or unsupported change.</param>
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public void FailedCommitPreservesAllStateAndAllowsCorrectedSameIdentity(int failure)
    {
        var rig = new Rig();
        var version = rig.Read().Version;
        var id = Guid.NewGuid();
        ICombatStateChange invalid = failure switch
        {
            0 => new HealthChanged(rig.Target.Id, 99, 90),
            1 => new HealthChanged(rig.Target.Id, 100, 101),
            2 => new CombatantDefeated(rig.Target.Id),
            _ => new UnsupportedChange()
        };
        var bad = Resolution(rig, id, version, [new HealthChanged(rig.Source.Id, 100, 95), invalid]);
        if (failure == 1) Assert.Throws<ArgumentOutOfRangeException>(() => rig.State.TryCommit(bad));
        else Assert.Throws<InvalidOperationException>(() => rig.State.TryCommit(bad));
        Assert.Equal(version, rig.Read().Version);
        Assert.Equal(100, rig.Read().Source.Health);
        Assert.Equal(100, rig.Read().Target.Health);
        var corrected = Resolution(rig, id, version,
            [new HealthChanged(rig.Source.Id, 100, 95), new HealthChanged(rig.Target.Id, 100, 90)]);
        Assert.Equal(CombatCommitStatus.Applied, rig.State.TryCommit(corrected));
        Assert.Equal(CombatCommitStatus.AlreadyApplied, rig.State.TryCommit(corrected));
        Assert.Equal(95, rig.Read().Source.Health);
        Assert.Equal(90, rig.Read().Target.Health);
        Assert.Equal(version + 1, rig.Read().Version);
    }

    /// <summary>A result cannot report defeat while the staged target remains alive.</summary>
    [Fact]
    public void ResultAndStateDefeatMustAgreeBeforePublication()
    {
        var rig = new Rig();
        var version = rig.Read().Version;
        var result = Resolution(rig, Guid.NewGuid(), version, [new HealthChanged(rig.Target.Id, 100, 50)], defeated: true);
        Assert.Throws<InvalidOperationException>(() => rig.State.TryCommit(result));
        Assert.Equal(100, rig.Read().Target.Health);
        Assert.Equal(version, rig.Read().Version);
    }

    /// <summary>A transaction cannot change another registered actor outside its source/target pair.</summary>
    [Fact]
    public void UnrelatedParticipantCannotBeChangedByResolution()
    {
        var rig = new Rig();
        var other = Character();
        rig.State.AddCombatant(other);
        var version = rig.Read().Version;
        var result = Resolution(rig, Guid.NewGuid(), version,
            [new HealthChanged(rig.Source.Id, 100, 95), new HealthChanged(other.Id, 100, 90)]);
        Assert.Throws<InvalidOperationException>(() => rig.State.TryCommit(result));
        Assert.Equal(100, rig.Read().Source.Health);
        Assert.Equal(100, rig.State.Read(other.Id, other.Id).Target.Health);
        Assert.Equal(version, rig.Read().Version);
    }

    /// <summary>A stale impact cannot publish health changes or consume its resolution identity.</summary>
    [Fact]
    public void StaleImpactDoesNotPublishChanges()
    {
        var rig = new Rig();
        var result = Resolution(rig, Guid.NewGuid(), rig.Read().Version, [new HealthChanged(rig.Target.Id, 100, 90)]);
        rig.State.AddCombatant(Character());
        Assert.Equal(CombatCommitStatus.VersionConflict, rig.State.TryCommit(result));
        Assert.Equal(100, rig.Read().Target.Health);
    }

    /// <summary>The basic factory and real adapter preserve captures and replace an existing status on another hit.</summary>
    [Fact]
    public void NewStatusApplicationAndReplacementAreCommitted()
    {
        var rig = new Rig(withEffect: true);
        var attack = rig.Start();
        var originalRead = rig.Read();
        Resolve(attack, rig.Target.Id);
        var first = Assert.Single(rig.Read().Target.Effects);
        Resolve(attack, rig.Target.Id);
        var second = Assert.Single(rig.Read().Target.Effects);
        Assert.NotEqual(first.Id, second.Id);
        Assert.Equal(rig.Source.Id, second.SourceSnapshot!.Id);
        Assert.Empty(originalRead.Target.Effects);
        Assert.Equal(80, rig.ReadStart().Resources.Single(value => value.Resource == Rig.Mana).Amount);
    }

    /// <summary>A self-target view is shared consistently and health is published once.</summary>
    [Fact]
    public void SelfTargetUsesOneViewAndOneHealthTransition()
    {
        var rig = new Rig();
        var before = rig.State.Read(rig.Source.Id, rig.Source.Id);
        Assert.Same(before.Source, before.Target);
        var result = Resolve(rig.Start(), rig.Source.Id);
        var health = Assert.Single(((ResolvedCombat)result.Outcome).Resolution.Changes.OfType<HealthChanged>());
        Assert.Equal(health.CurrentHealth, rig.Read().Source.Health);
    }

    /// <summary>A periodic proposal can commit against a live recipient when its captured source is not registered.</summary>
    [Fact]
    public void TickCanCommitWithoutALiveSourceEntry()
    {
        var source = Character(0);
        var definition = new PeriodicDamageEffectDefinition(new(Guid.NewGuid()), [new(new(1, 6), 1, DamageType.Fire)]);
        var effect = new CombatEffectSnapshot(new(Guid.NewGuid()), definition, source);
        var target = new CombatantSnapshot(new(Guid.NewGuid()), 100, 100, 0, 0, [], [effect]);
        var state = new InMemoryCombatState();
        state.AddCombatant(target);
        var read = state.Read(target.Id, target.Id);
        var request = new CombatRequest(Guid.NewGuid(), effect, read.Target, read.Version);
        var pipeline = new CombatPipeline([new RawDamageStep(new RawDamageResolver()), new DefenseStep(new DefenseResolver(new HitMagnitudeArmorFormula()))]);
        var outcome = LogOutcome(new CombatResolver(new CombatRequestValidator(), pipeline,
            new CombatResolutionBuilder(new HealthStateProjector())).Resolve(request, new RandomDiceRoller(new Random(42))));
        var resolution = Assert.IsType<ResolvedCombat>(outcome).Resolution;
        Assert.Equal(CombatCommitStatus.Applied, state.TryCommit(resolution));
        Assert.Equal(Assert.Single(resolution.Changes.OfType<HealthChanged>()).CurrentHealth, state.Read(target.Id, target.Id).Target.Health);
    }

    /// <summary>Registration copies balances and neither duplicate registration nor another session can overwrite state.</summary>
    [Fact]
    public void RegistrationAndSessionOwnershipAreIsolated()
    {
        var rig = new Rig();
        var otherSession = new InMemoryCombatState();
        var pools = new[] { new ResourceAmount(Rig.Mana, 50) };
        otherSession.AddCombatant(rig.Source, pools);
        otherSession.AddCombatant(rig.Target);
        pools[0] = new(Rig.Mana, 0);
        var version = rig.Read().Version;
        Assert.Throws<ArgumentException>(() => rig.State.AddCombatant(rig.Source));
        Assert.Equal(version, rig.Read().Version);
        Resolve(rig.Start(), rig.Target.Id);
        Assert.Equal(100, otherSession.Read(rig.Source.Id, rig.Target.Id).Target.Health);
        Assert.Equal(50, otherSession.ReadForStart(rig.Source.Id, "runtime", Guid.NewGuid()).Resources.Single().Amount);
    }

    /// <summary>Fresh trace objects share an explicitly supplied random stream without resetting its seed.</summary>
    [Fact]
    public void FreshDiceSourcesContinueOneStreamAndKeepIndependentTraces()
    {
        var random = new Random(42);
        var expected = new Random(42);
        var first = new RandomDiceRoller(random);
        var second = new RandomDiceRoller(random);
        var snapshot = first.Trace;
        Assert.Equal(expected.NextInt64(1, 21), first.Roll(new(1, 20)).Total);
        Assert.Equal(expected.NextInt64(1, 21), second.Roll(new(1, 20)).Total);
        Assert.Empty(snapshot.Rolls);
        Assert.Single(first.Trace.Rolls);
        Assert.Single(second.Trace.Rolls);
    }

    /// <summary>Runtime randomness supports valid inclusive die bounds including the largest side count.</summary>
    /// <param name="count">Number of dice.</param>
    /// <param name="sides">Number of sides.</param>
    [Theory]
    [InlineData(2, 6)]
    [InlineData(1, int.MaxValue)]
    [InlineData(5, 20)]
    public void RuntimeDiceValuesRespectFormula(int count, int sides)
    {
        var dice = new RandomDiceRoller(new Random(42));
        var result = dice.Roll(new(count, sides));
        Assert.Equal(count, result.Results.Count);
        Assert.All(result.Results, value => Assert.InRange(value, 1, sides));
        Assert.Equal(result.Results.Sum(), result.Total);
    }

    /// <summary>The largest inclusive die value is representable, while an overflowing total is not recorded as a valid roll.</summary>
    [Fact]
    public void RuntimeDiceHandleUpperBoundWithoutWrapping()
    {
        var dice = new RandomDiceRoller(new MaximumRandom());
        Assert.Equal(int.MaxValue, dice.Roll(new(1, int.MaxValue)).Total);
        Assert.Throws<OverflowException>(() => dice.Roll(new(2, int.MaxValue)));
        Assert.Single(dice.Trace.Rolls);
    }

    /// <summary>Explicit host edits refresh impact views without resetting resources, statuses, cooldowns or receipts.</summary>
    [Fact]
    public void HostStatEditsPreserveSessionBookkeepingAndInvalidateOldProposals()
    {
        var rig = new Rig(withEffect: true);
        var request = Guid.NewGuid();
        var start = rig.Starts.Start(request, rig.Source.Id, rig.Definition);
        Resolve(start.Attack!, rig.Target.Id);
        var before = rig.Read();
        var stale = Resolution(rig, Guid.NewGuid(), before.Version, []);
        rig.State.UpdateCombatant(new(rig.Target.Id, 40, 120, 12, 15,
            [new(DamageType.Fire, 0.25m)], before.Target.Effects));
        var after = rig.Read();
        Assert.Equal(before.Version + 1, after.Version);
        Assert.Equal(40, after.Target.Health);
        Assert.Equal(120, after.Target.MaxHealth);
        Assert.Equal(12, after.Target.Armor);
        Assert.Equal(15, after.Target.Evasion);
        Assert.Equal(0.25m, Assert.Single(after.Target.Resistances).Value);
        Assert.Same(Assert.Single(before.Target.Effects), Assert.Single(after.Target.Effects));
        Assert.NotEqual(40, before.Target.Health);
        Assert.Equal(80, rig.ReadStart().Resources.Single(value => value.Resource == Rig.Mana).Amount);
        Assert.Equal(TimeSpan.FromSeconds(2), rig.ReadStart().ReadyAt);
        Assert.Equal(AttackStartStatus.AlreadyStarted, rig.Starts.Start(request, rig.Source.Id, rig.Definition).Status);
        Assert.Equal(CombatCommitStatus.VersionConflict, rig.State.TryCommit(stale));
    }

    /// <summary>Unknown and null host edits cannot insert actors or advance the session version.</summary>
    [Fact]
    public void InvalidHostEditsLeaveStateUnchanged()
    {
        var rig = new Rig();
        var before = rig.Read();
        Assert.Throws<ArgumentNullException>(() => rig.State.UpdateCombatant(null!));
        Assert.Throws<KeyNotFoundException>(() => rig.State.UpdateCombatant(Character()));
        Assert.Equal(before.Version, rig.Read().Version);
        Assert.Same(before.Target, rig.Read().Target);
    }

    /// <summary>Factory-selected lifesteal and victim damage publish together using actual loss instead of overkill.</summary>
    [Fact]
    public void SelectedLifestealCommitsBothHealthTransitionsAtomically()
    {
        var rig = new Rig();
        rig.State.UpdateCombatant(new(rig.Source.Id, 50, 100, 0, 0, [], []));
        rig.State.UpdateCombatant(new(rig.Target.Id, 3, 100, 0, 0, [], []));
        var payload = rig.Definition.Attack;
        var definition = new AttackDefinition("vamp", new(payload.AttackRoll, 30, payload.OutcomeShiftRules,
            [new(new(1, 2), 30, DamageType.Fire)], [], payload.Tags, [new LifestealDefinition(0.5m)]),
            rig.Definition.Cooldown, rig.Definition.BaseCosts);
        var attack = rig.Starts.Start(Guid.NewGuid(), rig.Source.Id, definition).Attack!;
        var version = rig.Read().Version;
        var impact = Resolve(attack, rig.Target.Id);
        Assert.True(impact.IsCommitted);
        Assert.Equal(52, rig.Read().Source.Health);
        Assert.Equal(0, rig.Read().Target.Health);
        Assert.Equal(version + 1, rig.Read().Version);
        Assert.Equal(2, Assert.IsType<ResolvedCombat>(impact.Outcome).Resolution.Changes.OfType<HealthChanged>().Count());
    }

    /// <summary>Unsupported behavior composition and unspecified stacking cannot be silently ignored.</summary>
    [Fact]
    public void BasicFactoryRejectsUnknownOrRepeatedBehaviorDefinitions()
    {
        var rig = new Rig();
        var payload = rig.Definition.Attack;
        var factory = new BasicAttackPipelineFactory(new DamageMultipliers(0, 0.5m, 1, 2));
        var impacts = new CombatImpactService(rig.State, new CombatSnapshotBuilder(), factory, () => new RandomDiceRoller());
        foreach (var behaviors in new AttackBehaviorDefinition[][]
                 { [new UnknownBehavior()], [new LifestealDefinition(0.25m), new LifestealDefinition(0.5m)] })
        {
            var attack = new AttackInstance(rig.Source.Id, new(payload.AttackRoll, payload.AttackModifier,
                payload.OutcomeShiftRules, payload.Damage, payload.Effects, payload.Tags, behaviors), impacts);
            Assert.Throws<InvalidOperationException>(() => factory.Create(attack, new(rig.Read().Version, rig.Source, rig.Target)));
        }
    }

    private sealed record UnknownBehavior : AttackBehaviorDefinition;

    private CombatImpactResult Resolve(AttackInstance attack, CombatantId target)
    {
        var result = attack.Resolve(target);
        LogOutcome(result.Outcome);
        return result;
    }

    private static AttackStartProposal StartProposal(Rig rig, IReadOnlyList<ResourceAmount> costs) =>
        new(Guid.NewGuid(), Guid.NewGuid(), rig.Source.Id, "runtime", rig.Read().Version,
            new ResourceCostResolver(new AdditiveResourceCostPolicy()).Resolve(costs, []), TimeSpan.FromSeconds(2));
    private static CombatResolution Resolution(Rig rig, Guid id, long version,
        IReadOnlyList<ICombatStateChange> changes, bool defeated = false) => new(id, version,
        new(rig.Source.Id, new(rig.Target.Id, null, null, null, [], defeated)), changes, new([]));
    private static CombatantSnapshot Character(int health = 100) => new(new(Guid.NewGuid()), health, 100, 0, 0, [], []);

    private sealed record UnsupportedChange : ICombatStateChange;
    private sealed class MaximumRandom : Random
    {
        /// <inheritdoc />
        public override long NextInt64(long minValue, long maxValue) => maxValue - 1;
    }

    private sealed class Rig
    {
        internal static readonly ResourceId Mana = new("mana");
        internal CombatantSnapshot Source { get; } = Character();
        internal CombatantSnapshot Target { get; } = Character();
        internal InMemoryCombatState State { get; } = new();
        internal AttackInitiationService Starts { get; }
        internal AttackDefinition Definition { get; }

        internal Rig(bool withEffect = false)
        {
            State.AddCombatant(Source, [new(Mana, 100)]);
            State.AddCombatant(Target);
            var random = new Random(42);
            var impacts = new CombatImpactService(State, new CombatSnapshotBuilder(),
                new BasicAttackPipelineFactory(new DamageMultipliers(0, 0.5m, 1, 2)), () => new RandomDiceRoller(random));
            Starts = new(State, new ResourceCostResolver(new AdditiveResourceCostPolicy()), new NoResourceCostModifiers(), impacts,
                () => TimeSpan.Zero);
            CombatEffectDefinition[] effects = withEffect
                ? [new PeriodicDamageEffectDefinition(new(Guid.NewGuid()), [new(new(1, 6), 0, DamageType.Fire)])] : [];
            Definition = new("runtime", new(new(1, 20), 30, NaturalOutcomeShiftRules.Standard,
                [new(new(1, 6), 3, DamageType.Slashing)], effects, AttackTags.Projectile), TimeSpan.FromSeconds(2), [new(Mana, 20)]);
        }

        internal AttackInstance Start() => Starts.Start(Guid.NewGuid(), Source.Id, Definition).Attack!;
        internal CombatStateRead Read() => State.Read(Source.Id, Target.Id);
        internal AttackInitiationState ReadStart() => State.ReadForStart(Source.Id, "runtime", Guid.NewGuid());
    }
}
