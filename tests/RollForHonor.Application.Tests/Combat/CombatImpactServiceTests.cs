using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using RollForHonor.Infrastructure.Diagnostics;
using RollForHonor.Application.Combat.Models;
using RollForHonor.Application.Combat.Ports;
using RollForHonor.Application.Combat.Services;
using RollForHonor.Domain.Combat.Attacks.Models;
using RollForHonor.Domain.Combat.Attacks.Services;
using RollForHonor.Domain.Combat.Damage.Configuration;
using RollForHonor.Domain.Combat.Damage.Models;
using RollForHonor.Domain.Combat.Damage.Services;
using RollForHonor.Domain.Combat.Effects.Models;
using RollForHonor.Domain.Combat.Effects.Services;
using RollForHonor.Domain.Combat.Resolution.Models;
using RollForHonor.Domain.Combat.Resolution.Services;
using RollForHonor.Domain.Combat.Resolution.Services.Steps;
using RollForHonor.Domain.Combat.State.Changes;
using RollForHonor.Domain.Combat.State.Models;
using RollForHonor.Domain.Dice.Models;
using RollForHonor.Domain.Dice.Services;
using RollForHonor.Domain.Tests.Diagnostics;
using Xunit.Abstractions;

namespace RollForHonor.Application.Tests.Combat;

public sealed class CombatImpactServiceTests(ITestOutputHelper output) : CombatDiagnosticTestBase(output)
{
    [Fact]
    public void OneAttack_MultipleTargets_UsesFreshSnapshotsIndependentDiceAndSeparateCommits()
    {
        var source = Character();
        var targets = new[] { Character(), Character(armor: 30),
            Character(resistances: [new ResistanceValue(DamageType.Slashing, 0.5m)]) };
        var rig = new Rig([source, .. targets]);
        var attack = new AttackInstance(source.Id, Payload(), rig.Service);
        var results = targets.Select(target => Resolve(attack, target.Id)).ToArray();

        Assert.Equal(new[] { 6, 3, 3 }, results.Select(result => Resolution(result).Result.Target.DamageResolution!.AfterDefenses.Sum()));
        Assert.Equal(new[] { 44, 47, 47 }, targets.Select(target => rig.State.Get(target.Id).Health));
        Assert.All(results, result =>
        {
            Assert.Equal(attack.Id, result.AttackId);
            Assert.True(result.IsCommitted);
            Assert.Equal(CombatCommitStatus.Applied, result.CommitStatus);
            Assert.Equal(result.ResolutionId, Resolution(result).ResolutionId);
            Assert.Equal(new[] { 20, 6 }, Resolution(result).DiceTrace.Rolls.Select(roll => roll.Formula.Sides));
        });
        Assert.Equal(3, results.Select(result => result.ResolutionId).Distinct().Count());
        Assert.Equal(new long[] { 0, 1, 2 }, rig.Snapshots.Reads.Select(read => read.Version));
        Assert.Equal(3, rig.Dice.Count);
        Assert.Equal(3, rig.State.Version);
        Assert.Equal(50, rig.State.Get(source.Id).Health);
        Assert.All(targets, target => Assert.Equal(50, target.Health));
    }

    [Fact]
    public void SourceBuffAddedAfterCreation_IsSelectedAtNextImpactWithoutRecreatingAttack()
    {
        var source = Character();
        var target = Character();
        var rig = new Rig(source, target);
        var attack = new AttackInstance(source.Id, Payload(), rig.Service);
        var first = Resolve(attack, target.Id);
        rig.State.ExternalUpdate(With(source, effects: [BonusStatus(5)]));
        var second = Resolve(attack, target.Id);

        Assert.Equal(6, Resolution(first).Result.Target.DamageResolution!.AfterDefenses.Sum());
        Assert.Equal(11, Resolution(second).Result.Target.DamageResolution!.AfterDefenses.Sum());
        Assert.Equal(33, rig.State.Get(target.Id).Health);
        Assert.Empty(rig.Snapshots.Reads[0].Source.Effects);
        Assert.Single(rig.Snapshots.Reads[1].Source.Effects);
        Assert.Equal(attack.Id, second.AttackId);
        Assert.Equal(new long[] { 0, 2 }, rig.State.Reads.Select(read => read.Version));
        Assert.Empty(source.Effects);
    }

    [Fact]
    public void SourceBuffRemovedAfterFirstImpact_IsNotRetainedByAttackInstance()
    {
        var source = Character(effects: [BonusStatus(5)]);
        var target = Character();
        var rig = new Rig(source, target);
        var attack = new AttackInstance(source.Id, Payload(), rig.Service);
        var first = Resolve(attack, target.Id);
        rig.State.ExternalUpdate(With(source, effects: []));
        var second = Resolve(attack, target.Id);

        Assert.Equal(11, Resolution(first).Result.Target.DamageResolution!.AfterDefenses.Sum());
        Assert.Equal(6, Resolution(second).Result.Target.DamageResolution!.AfterDefenses.Sum());
        Assert.Equal(33, rig.State.Get(target.Id).Health);
        Assert.Single(rig.Snapshots.Reads[0].Source.Effects);
        Assert.Empty(rig.Snapshots.Reads[1].Source.Effects);
    }

    [Fact]
    public void TargetResistanceChangedBetweenHits_IsUsedOnNextImpact()
    {
        var source = Character();
        var target = Character();
        var rig = new Rig(source, target);
        var attack = new AttackInstance(source.Id, Payload(), rig.Service);
        Resolve(attack, target.Id);
        rig.State.ExternalUpdate(With(rig.State.Get(target.Id),
            resistances: [new ResistanceValue(DamageType.Slashing, 0.5m)]));
        var second = Resolve(attack, target.Id);

        Assert.Equal(44, rig.Snapshots.Reads[1].Target.Health);
        Assert.Equal(3, Resolution(second).Result.Target.DamageResolution!.AfterDefenses.Sum());
        Assert.Equal(41, rig.State.Get(target.Id).Health);
    }

    [Fact]
    public void SuccessiveHits_CommitHealthAndDefeatAgainstFreshVersions()
    {
        var source = Character();
        var target = Character(15);
        var rig = new Rig(source, target);
        var attack = new AttackInstance(source.Id, Payload(), rig.Service);
        for (var index = 0; index < 3; index++)
        {
            var result = Resolve(attack, target.Id);
            Assert.True(result.IsCommitted);
            Assert.Equal(index, Resolution(result).BasedOnStateVersion);
            Assert.Equal(new[] { 9, 3, 0 }[index], rig.State.Get(target.Id).Health);
            Assert.Equal(index == 2, Resolution(result).Changes.OfType<CombatantDefeated>().Any());
        }
        Assert.Single(rig.State.Committed.SelectMany(result => result.Changes.OfType<CombatantDefeated>()));
    }

    [Fact]
    public void AppliedStatuses_AreVisibleToNextSnapshot_AndReplacementCommitsRemovalAndAddition()
    {
        var source = Character();
        var target = Character();
        var rig = new Rig(source, target);
        var attack = new AttackInstance(source.Id, Payload(Burning()), rig.Service);
        var first = Resolve(attack, target.Id);
        var old = Assert.Single(rig.State.Get(target.Id).Effects);
        var second = Resolve(attack, target.Id);

        Assert.Empty(rig.Snapshots.Reads[0].Target.Effects);
        Assert.Same(old, Assert.Single(rig.Snapshots.Reads[1].Target.Effects));
        Assert.Equal(old.Id, Assert.Single(Resolution(second).Changes.OfType<EffectRemoved>()).EffectId);
        Assert.NotEqual(old.Id, Assert.Single(rig.State.Get(target.Id).Effects).Id);
        Assert.Equal(38, rig.State.Get(target.Id).Health);
        Assert.Equal(2, rig.State.Version);
        Assert.True(first.IsCommitted && second.IsCommitted);
    }

    [Fact]
    public void VersionConflict_PublishesNoOwnChangesAndDoesNotRetryOrReroll()
    {
        var source = Character();
        var target = Character();
        var rig = new Rig(source, target);
        var attack = new AttackInstance(source.Id, Payload(Burning()), rig.Service);
        rig.State.BeforeCommit = _ =>
        {
            rig.State.BeforeCommit = null;
            rig.State.ExternalUpdate(With(source, effects: [BonusStatus(5)]));
        };
        var conflict = Resolve(attack, target.Id);

        Assert.False(conflict.IsCommitted);
        Assert.Equal(CombatCommitStatus.VersionConflict, conflict.CommitStatus);
        Assert.IsType<ResolvedCombat>(conflict.Outcome); // calculated is not committed
        Assert.Empty(rig.State.Committed);
        Assert.Equal(50, rig.State.Get(target.Id).Health);
        Assert.Empty(rig.State.Get(target.Id).Effects);
        Assert.Single(rig.Dice);
        Assert.Single(rig.State.Reads);
        Assert.Equal(1, rig.State.CommitAttempts);

        // Caller explicitly starts another attempt with fresh input and a new resolution identity.
        var fresh = Resolve(attack, target.Id);
        Assert.True(fresh.IsCommitted);
        Assert.NotEqual(conflict.ResolutionId, fresh.ResolutionId);
        Assert.Equal(1, Resolution(fresh).BasedOnStateVersion);
        Assert.Equal(11, Resolution(fresh).Result.Target.DamageResolution!.AfterDefenses.Sum());
        Assert.Equal(39, rig.State.Get(target.Id).Health);
        Assert.Single(rig.State.Get(target.Id).Effects);
    }

    [Fact]
    public void DomainRejection_DoesNotReachCommitOrRollDice()
    {
        var source = Character(0);
        var target = Character();
        var rig = new Rig(source, target);
        var result = Resolve(new AttackInstance(source.Id, Payload(), rig.Service), target.Id);

        Assert.Equal(CombatRejectionReason.SourceDefeated, Assert.IsType<RejectedCombat>(result.Outcome).Rejection.Reason);
        Assert.Null(result.CommitStatus);
        Assert.False(result.IsCommitted);
        Assert.Equal(0, rig.State.CommitAttempts);
        Assert.Empty(Assert.Single(rig.Dice).Trace.Rolls);
        Assert.Equal(0, rig.State.Version);
        Assert.Equal(50, rig.State.Get(target.Id).Health);
    }

    [Fact]
    public void PipelineFailureAfterProposals_DoesNotCallCommit()
    {
        var source = Character();
        var target = Character();
        var rig = new Rig(source, target);
        rig.Pipelines.FailAfterProposals = true;
        var attack = new AttackInstance(source.Id, Payload(Burning()), rig.Service);

        Assert.Throws<InvalidOperationException>(() => attack.Resolve(target.Id));
        Assert.Equal(0, rig.State.CommitAttempts);
        Assert.Equal(0, rig.State.Version);
        Assert.Equal(50, rig.State.Get(target.Id).Health);
        Assert.Empty(rig.State.Get(target.Id).Effects);
    }

    [Fact]
    public void AdapterFailureAfterStaging_PublishesNeitherHpNorStatus()
    {
        var source = Character();
        var target = Character();
        var rig = new Rig(source, target);
        rig.State.FailAfterStaging = true;
        var attack = new AttackInstance(source.Id, Payload(Burning()), rig.Service);

        Assert.Throws<InvalidOperationException>(() => attack.Resolve(target.Id));
        Assert.Equal(1, rig.State.CommitAttempts);
        Assert.Empty(rig.State.Committed);
        Assert.Equal(0, rig.State.Version);
        Assert.Equal(50, rig.State.Get(target.Id).Health);
        Assert.Empty(rig.State.Get(target.Id).Effects);
    }

    [Fact]
    public void SourceHealingAndTargetDamage_ArePublishedTogetherAtOneVersion()
    {
        var source = Character(20);
        var target = Character();
        var rig = new Rig(source, target);
        rig.Pipelines.HealSource = true;
        rig.State.BeforeCommit = _ =>
        {
            Assert.Equal(20, rig.State.Get(source.Id).Health);
            Assert.Equal(50, rig.State.Get(target.Id).Health);
        };
        var result = Resolve(new AttackInstance(source.Id, Payload(), rig.Service), target.Id);

        Assert.Equal(2, Resolution(result).Changes.OfType<HealthChanged>().Count());
        Assert.True(result.IsCommitted);
        Assert.Equal(23, rig.State.Get(source.Id).Health);
        Assert.Equal(44, rig.State.Get(target.Id).Health);
        Assert.Equal(1, rig.State.Version);
        Assert.Equal(20, source.Health);
        Assert.Equal(50, target.Health);
    }

    [Fact]
    public void ReplayOfCommittedResolution_IsRejectedByAdapterWithoutRepeatingChanges()
    {
        var source = Character();
        var target = Character();
        var rig = new Rig(source, target);
        var result = Resolve(new AttackInstance(source.Id, Payload(Burning()), rig.Service), target.Id);
        var replay = rig.State.TryCommit(Resolution(result));

        Assert.Equal(CombatCommitStatus.AlreadyApplied, replay);
        Assert.Equal(44, rig.State.Get(target.Id).Health);
        Assert.Single(rig.State.Get(target.Id).Effects);
        Assert.Equal(1, rig.State.Version);
        Assert.Single(rig.State.Committed);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public void InconsistentBuilderOutput_IsRejectedBeforeDiceOrCommit(int mismatch)
    {
        var source = Character();
        var target = Character();
        var rig = new Rig(source, target);
        rig.Snapshots.Override = read => new CombatStateSnapshot(read.Version + (mismatch == 0 ? 1 : 0),
            mismatch == 1 ? Character() : read.Source, mismatch == 2 ? Character() : read.Target);
        var attack = new AttackInstance(source.Id, Payload(), rig.Service);

        Assert.Throws<InvalidOperationException>(() => attack.Resolve(target.Id));
        Assert.Empty(rig.Dice);
        Assert.Equal(0, rig.State.CommitAttempts);
        Assert.Equal(0, rig.State.Version);
    }

    [Fact]
    public void SelfTarget_ReadAndSnapshotShareCurrentParticipant_AndHpIsChangedOnce()
    {
        var character = Character();
        var rig = new Rig(character);
        var attack = new AttackInstance(character.Id, Payload(), rig.Service);
        var result = Resolve(attack, character.Id);

        var read = Assert.Single(rig.Snapshots.Reads);
        Assert.Same(read.Source, read.Target);
        Assert.Single(Resolution(result).Changes.OfType<HealthChanged>());
        Assert.Equal(44, rig.State.Get(character.Id).Health);
        Assert.Equal(50, character.Health);
    }

    [Fact]
    public void ReusedDiceTrace_IsRejectedBeforeSecondCalculationOrCommit()
    {
        var source = Character();
        var target = Character();
        var rig = new Rig(source, target);
        var attack = new AttackInstance(source.Id, Payload(), rig.Service);
        Resolve(attack, target.Id);
        rig.DiceOverride = () => rig.Dice[0];

        Assert.Throws<InvalidOperationException>(() => attack.Resolve(target.Id));
        Assert.Equal(1, rig.State.CommitAttempts);
        Assert.Equal(44, rig.State.Get(target.Id).Health);
        Assert.Equal(2, rig.Dice[0].Trace.Rolls.Count);
    }

    private CombatImpactResult Resolve(AttackInstance attack, CombatantId target)
    {
        var result = attack.Resolve(target);
        LogOutcome(result.Outcome);
        return result;
    }

    private static CombatResolution Resolution(CombatImpactResult result) => Assert.IsType<ResolvedCombat>(result.Outcome).Resolution;
    private static CombatantSnapshot Character(int health = 50, int armor = 0,
        IReadOnlyList<ResistanceValue>? resistances = null, IReadOnlyList<CombatEffectSnapshot>? effects = null) =>
        new(new CombatantId(Guid.NewGuid()), health, 100, armor, 15, resistances ?? [], effects ?? []);

    private static CombatantSnapshot With(CombatantSnapshot state,
        IReadOnlyList<CombatEffectSnapshot>? effects = null, IReadOnlyList<ResistanceValue>? resistances = null) =>
        new(state.Id, state.Health, state.MaxHealth, state.Armor, state.Evasion,
            resistances ?? state.Resistances, effects ?? state.Effects);

    private static AttackPayload Payload(params CombatEffectDefinition[] effects) =>
        new(new DiceFormula(1, 20), 0, NaturalOutcomeShiftRules.Standard,
            [new DamageComponent(new DiceFormula(1, 6), 3, DamageType.Slashing)], effects, AttackTags.Projectile);

    private static PeriodicDamageEffectDefinition Burning() =>
        new(new EffectDefinitionId(Guid.NewGuid()), [new DamageComponent(new DiceFormula(1, 6), 3, DamageType.Fire)]);

    private static CombatEffectSnapshot BonusStatus(int bonus) =>
        new(new EffectInstanceId(Guid.NewGuid()), new BonusDefinition(bonus));

    private sealed record BonusDefinition : CombatEffectDefinition
    {
        public int Bonus { get; }
        public BonusDefinition(int bonus) : base(new EffectDefinitionId(Guid.NewGuid())) => Bonus = bonus;
    }

    /// <summary>Checks every view against the same committed damage, nested effects and complete concrete JSON data.</summary>
    /// <param name="view">The diagnostic representation being checked.</param>
    [Theory]
    [InlineData(CombatDiagnosticView.Summary)]
    [InlineData(CombatDiagnosticView.Steps)]
    [InlineData(CombatDiagnosticView.Full)]
    [InlineData(CombatDiagnosticView.Jsonl)]
    public void Diagnostics_AllViewsKeepDiceAndCommitsAndExportConcreteChanges(CombatDiagnosticView view)
    {
        var source = Character(effects: [BonusStatus(5)]);
        var target = Character(armor: 30);
        var baseline = new Rig(source, target);
        var sink = new RecordingDiagnostics();
        var logged = new Rig(source, target) { Diagnostics = sink };
        var expected = Resolution(Resolve(new AttackInstance(source.Id, Payload(), baseline.Service), target.Id));
        var actual = Resolution(Resolve(new AttackInstance(source.Id, Payload(), logged.Service), target.Id));
        Assert.Equal(expected.Result.Target.DamageResolution!.AfterDefenses.Sum(), actual.Result.Target.DamageResolution!.AfterDefenses.Sum());
        Assert.Equal(baseline.State.Get(target.Id), logged.State.Get(target.Id));
        Assert.Equal(expected.DiceTrace.Rolls.SelectMany(r => r.Results), actual.DiceTrace.Rolls.SelectMany(r => r.Results));
        var report = Assert.Single(sink.Reports);
        Assert.Contains(report.Steps, step => step.Depth == 1 && step.Name == nameof(BonusDamageEffect));
        Assert.Contains(report.Steps, step => step.Name == "ProjectHealth" && step.Details is not null);
        Assert.Contains(report.Steps, step => step.Before.BeforeDefenses?.Sum() == 6 && step.After?.BeforeDefenses?.Sum() == 11);
        var text = CombatDiagnosticFormatter.Format(report, view);
        Assert.Contains(report.AttackId.ToString(), text);
        LogDiagnosticText(text);
        using var json = JsonDocument.Parse(CombatDiagnosticFormatter.Format(report, CombatDiagnosticView.Jsonl));
        var resolution = json.RootElement.GetProperty("result").GetProperty("outcome").GetProperty("resolution");
        Assert.True(resolution.GetProperty("changes")[0].TryGetProperty("currentHealth", out _));
        Assert.Equal("HealthChanged", resolution.GetProperty("changes")[0].GetProperty("$type").GetString());
        Assert.Equal(new[] { 15, 3 }, json.RootElement.GetProperty("dice").GetProperty("rolls").EnumerateArray()
            .SelectMany(r => r.GetProperty("results").EnumerateArray().Select(d => d.GetInt32())));
        Assert.True(json.RootElement.GetProperty("result").GetProperty("isCommitted").GetBoolean());
        if (view == CombatDiagnosticView.Steps) Assert.Contains("[15] = 15", text);
        if (view == CombatDiagnosticView.Full) Assert.Contains("bonus: 5", text);
    }

    /// <summary>Checks that diagnostic configuration and output exceptions cannot change a committed hit.</summary>
    /// <param name="failConfiguration">Whether diagnostic enablement throws instead of report output.</param>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Diagnostics_ConfigurationOrWriteFailureDoesNotChangeCommit(bool failConfiguration)
    {
        var source = Character();
        var target = Character();
        var rig = new Rig(source, target) { Diagnostics = new FailingDiagnostics(failConfiguration) };
        var result = Resolve(new AttackInstance(source.Id, Payload(), rig.Service), target.Id);
        Assert.True(result.IsCommitted);
        Assert.Equal(44, rig.State.Get(target.Id).Health);
        Assert.Equal(2, Resolution(result).DiceTrace.Rolls.Count);
    }

    /// <summary>Retains completed values and dice alongside the failed nested effect without committing state.</summary>
    [Fact]
    public void Diagnostics_PipelineFailureRetainsCompletedStepsDiceAndFailingNestedEffect()
    {
        var source = Character();
        var target = Character();
        var sink = new RecordingDiagnostics();
        var rig = new Rig(source, target) { Diagnostics = sink };
        rig.Pipelines.FailAfterProposals = true;
        var attack = new AttackInstance(source.Id, Payload(effects: [Burning()]), rig.Service);
        Assert.Throws<InvalidOperationException>(() => attack.Resolve(target.Id));
        var report = Assert.Single(sink.Reports);
        Assert.Equal("Calculate", report.Phase);
        Assert.Contains(report.Steps, step => step.Name == nameof(FailEffect) && step.Error is not null);
        Assert.Contains(report.Steps, step => step.Name == nameof(RawDamageStep) && step.After?.BeforeDefenses?.Sum() == 6);
        Assert.Equal(2, report.Dice!.Rolls.Count);
        Assert.Equal(50, rig.State.Get(target.Id).Health);
        Assert.Equal(0, rig.State.CommitAttempts);
    }

    /// <summary>Exports Domain rejection without a commit attempt, intermediate steps or consumed dice.</summary>
    [Fact]
    public void Diagnostics_RejectionDoesNotClaimCommitOrConsumeDice()
    {
        var source = Character(health: 0);
        var target = Character();
        var sink = new RecordingDiagnostics();
        var rig = new Rig(source, target) { Diagnostics = sink };
        var result = new AttackInstance(source.Id, Payload(), rig.Service).Resolve(target.Id);
        Assert.False(result.IsCommitted);
        var report = Assert.Single(sink.Reports);
        Assert.Empty(report.Steps);
        Assert.Empty(report.Dice!.Rolls);
        Assert.Contains("Rejected", CombatDiagnosticFormatter.Format(report, CombatDiagnosticView.Summary));
        using var json = JsonDocument.Parse(CombatDiagnosticFormatter.Format(report, CombatDiagnosticView.Jsonl));
        Assert.True(json.RootElement.GetProperty("result").GetProperty("outcome").TryGetProperty("rejection", out _));
    }

    /// <summary>Keeps a stale calculated proposal separate from unchanged target health.</summary>
    [Fact]
    public void Diagnostics_ConflictIsErrorAndProposalIsNotPresentedAsApplied()
    {
        var source = Character();
        var target = Character();
        var sink = new RecordingDiagnostics();
        var rig = new Rig(source, target) { Diagnostics = sink };
        rig.State.BeforeCommit = _ => rig.State.ExternalUpdate(source);
        var result = new AttackInstance(source.Id, Payload(), rig.Service).Resolve(target.Id);
        Assert.Equal(CombatCommitStatus.VersionConflict, result.CommitStatus);
        var report = Assert.Single(sink.Reports);
        Assert.True(report.IsError);
        Assert.Contains("VersionConflict", CombatDiagnosticFormatter.Format(report, CombatDiagnosticView.Summary));
        Assert.Equal(50, rig.State.Get(target.Id).Health);
    }

    /// <summary>Checks independent JSONL delivery and combined identity and outcome filters.</summary>
    [Fact]
    public void Diagnostics_IndependentJsonlSurvivesLoggerFailureAndHonorsFilters()
    {
        var source = Character();
        var target = Character();
        using var jsonl = new StringWriter();
        var logger = new ThrowingLogger();
        var diagnostics = new LoggerCombatDiagnostics(logger, filter: new(SourceId: source.Id, TargetId: target.Id), jsonl: jsonl);
        var rig = new Rig(source, target) { Diagnostics = diagnostics };
        var attack = new AttackInstance(source.Id, Payload(), rig.Service);
        Assert.False(diagnostics.IsEnabled(attack.Id, target.Id, source.Id));
        Assert.True(attack.Resolve(target.Id).IsCommitted);
        var line = Assert.Single(jsonl.ToString().Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries));
        using var parsed = JsonDocument.Parse(line);
        Assert.Equal("impact", parsed.RootElement.GetProperty("kind").GetString());
        Assert.False(new LoggerCombatDiagnostics(NullLogger.Instance).IsEnabled(attack.Id, source.Id, target.Id));
        Assert.False(new LoggerCombatDiagnostics(NullLogger.Instance, filter: new(AttackId: Guid.NewGuid()), jsonl: jsonl)
            .IsEnabled(attack.Id, source.Id, target.Id));
        using var errors = new StringWriter();
        var filtered = new LoggerCombatDiagnostics(NullLogger.Instance, filter: new(ErrorsOnly: true), jsonl: errors);
        var healthyReport = new RecordingDiagnostics();
        var healthy = new Rig(source, target) { Diagnostics = healthyReport };
        new AttackInstance(source.Id, Payload(), healthy.Service).Resolve(target.Id);
        filtered.Write(Assert.Single(healthyReport.Reports));
        Assert.Equal("", errors.ToString());
    }

    /// <summary>Retains a completed calculation when staged commit throws without publishing health changes.</summary>
    [Fact]
    public void Diagnostics_CommitFailureExportsProposalWithoutClaimingAppliedState()
    {
        var source = Character();
        var target = Character();
        var sink = new RecordingDiagnostics();
        var rig = new Rig(source, target) { Diagnostics = sink };
        rig.State.FailAfterStaging = true;
        Assert.Throws<InvalidOperationException>(() => new AttackInstance(source.Id, Payload(), rig.Service).Resolve(target.Id));
        var report = Assert.Single(sink.Reports);
        Assert.Equal("Commit", report.Phase);
        Assert.Null(report.Result);
        Assert.IsType<ResolvedCombat>(report.Calculation);
        Assert.Contains(report.Steps, step => step.Name == "BuildStateChanges" && step.Details is not null);
        Assert.Equal(50, rig.State.Get(target.Id).Health);
    }

    /// <summary>Exports participant identities on failed reads without fabricating unavailable state or rolls.</summary>
    [Fact]
    public void Diagnostics_ReadFailureHasIdentitiesWithoutInventingSnapshotOrDice()
    {
        var source = Character();
        var sink = new RecordingDiagnostics();
        var rig = new Rig(source) { Diagnostics = sink };
        var target = new CombatantId(Guid.NewGuid());
        Assert.Throws<KeyNotFoundException>(() => new AttackInstance(source.Id, Payload(), rig.Service).Resolve(target));
        var report = Assert.Single(sink.Reports);
        Assert.Equal("Read", report.Phase);
        Assert.Equal(target, report.TargetId);
        Assert.Null(report.Request);
        Assert.Null(report.Dice);
        Assert.Empty(rig.Dice);
    }

    /// <summary>Checks that a failed JSONL writer cannot undo a newly applied hit.</summary>
    [Fact]
    public void Diagnostics_FailedJsonlWriterDoesNotChangeAppliedState()
    {
        var source = Character();
        var target = Character();
        var rig = new Rig(source, target)
        {
            Diagnostics = new LoggerCombatDiagnostics(NullLogger.Instance, jsonl: new FailingWriter())
        };
        Assert.True(new AttackInstance(source.Id, Payload(), rig.Service).Resolve(target.Id).IsCommitted);
        Assert.Equal(44, rig.State.Get(target.Id).Health);
    }

    /// <summary>Preserves every die, supplied flat bonus, attack modifier and damage-step dice range.</summary>
    [Fact]
    public void Diagnostics_MultiDiceComponentsRetainEveryDieFlatBonusAndRange()
    {
        var source = Character();
        var target = Character();
        var sink = new RecordingDiagnostics();
        var rig = new Rig(source, target) { Diagnostics = sink, DiceOverride = () => new ScriptedDice(15, 2, 5, 4) };
        var payload = new AttackPayload(new(1, 20), 2, NaturalOutcomeShiftRules.Standard,
            [new(new(2, 6), 3, DamageType.Slashing), new(new(1, 4), 0, DamageType.Fire)], [], AttackTags.Projectile);
        var result = Resolve(new AttackInstance(source.Id, payload, rig.Service), target.Id);
        Assert.Equal(36, rig.State.Get(target.Id).Health);
        var report = Assert.Single(sink.Reports);
        Assert.Equal(new[] { 15, 2, 5, 4 }, report.Dice!.Rolls.SelectMany(roll => roll.Results));
        var damage = Assert.Single(report.Steps, step => step.Name == nameof(RawDamageStep));
        Assert.Equal((1, 3), (damage.DiceStartIndex, damage.DiceEndIndex));
        Assert.Equal(3, report.Request!.Damage[0].FlatDamage);
        Assert.Equal(2, report.Request.Attack!.AttackModifier);
        Assert.Contains(Resolution(result).Result.Target.DamageResolution!.BeforeDefenses.Breakdown.Steps,
            step => step.Type == DamageStepType.AttackQuality && step.Multiplier == 1);
    }

    /// <summary>Checks that concurrent writes to a shared writer remain separate valid JSON objects.</summary>
    [Fact]
    public void Diagnostics_ConcurrentRecordsRemainSeparateValidJsonLines()
    {
        var source = Character();
        var target = Character();
        var sink = new RecordingDiagnostics();
        var rig = new Rig(source, target) { Diagnostics = sink };
        Resolve(new AttackInstance(source.Id, Payload(), rig.Service), target.Id);
        var report = Assert.Single(sink.Reports);
        using var writer = new StringWriter();
        var diagnostics = new LoggerCombatDiagnostics(NullLogger.Instance, jsonl: writer);
        Parallel.For(0, 30, _ => diagnostics.Write(report));
        var lines = writer.ToString().Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries);
        Assert.Equal(30, lines.Length);
        Assert.All(lines, line =>
        {
            using var parsed = JsonDocument.Parse(line);
            Assert.Equal(report.ResolutionId, parsed.RootElement.GetProperty("resolutionId").GetGuid());
        });
    }

    /// <summary>Captures a direct root step while preserving the pre-existing Domain execution trace.</summary>
    [Fact]
    public void Diagnostics_DirectRootStepIsCapturedWithoutChangingDomainExecutionTrace()
    {
        var source = Character();
        var target = Character();
        var state = new TestCombatStateAdapter(source, target);
        var sink = new RecordingDiagnostics();
        var service = new CombatImpactService(state, new CombatSnapshotBuilder(), new DirectFactory(),
            () => new ScriptedDice(), sink);
        var result = Resolve(new AttackInstance(source.Id, Payload(), service), target.Id);
        var report = Assert.Single(sink.Reports);
        Assert.Contains(report.Steps, step => step.Name == "DirectHealth" && step.After!.HealthAdjustments.Single().Amount == -2);
        Assert.Equal(48, state.Get(target.Id).Health);
        Assert.Single(Resolution(result).StepTrace);
    }

    private sealed class DirectFactory : ICombatPipelineFactory
    {
        /// <inheritdoc />
        public ICombatStep Create(AttackInstance attack, CombatStateSnapshot snapshot) => new DirectStep();
    }

    private sealed class DirectStep : ICombatStep
    {
        /// <inheritdoc />
        public string Name => "DirectHealth";
        /// <inheritdoc />
        public CombatContext Execute(CombatContext context, IDiceRoller dice) => context with
        {
            HealthAdjustments = [new(context.Request.TargetId, -2)]
        };
    }

    private sealed class FailingWriter : StringWriter
    {
        /// <inheritdoc />
        public override void WriteLine(string? value) => throw new IOException("Disk failed");
    }

    private sealed class RecordingDiagnostics : ICombatDiagnostics
    {
        public List<CombatDiagnosticReport> Reports { get; } = [];
        /// <inheritdoc />
        public bool IsEnabled(Guid attackId, CombatantId sourceId, CombatantId targetId) => true;
        /// <inheritdoc />
        public bool IsStartEnabled(CombatantId sourceId) => true;
        /// <inheritdoc />
        public void Write(CombatDiagnosticReport report) => Reports.Add(report);
        /// <inheritdoc />
        public void Write(AttackStartDiagnosticReport report) { }
    }

    private sealed class FailingDiagnostics(bool failConfiguration) : ICombatDiagnostics
    {
        /// <inheritdoc />
        public bool IsEnabled(Guid attackId, CombatantId sourceId, CombatantId targetId) =>
            failConfiguration ? throw new IOException("Configuration failed") : true;
        /// <inheritdoc />
        public bool IsStartEnabled(CombatantId sourceId) => true;
        /// <inheritdoc />
        public void Write(CombatDiagnosticReport report) => throw new IOException("Output failed");
        /// <inheritdoc />
        public void Write(AttackStartDiagnosticReport report) => throw new IOException("Output failed");
    }

    private sealed class ThrowingLogger : ILogger
    {
        /// <inheritdoc />
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        /// <inheritdoc />
        public bool IsEnabled(LogLevel level) => true;
        /// <inheritdoc />
        public void Log<TState>(LogLevel level, EventId id, TState state, Exception? error,
            Func<TState, Exception?, string> formatter) => throw new IOException("Logger failed");
    }

    private sealed class Rig(params CombatantSnapshot[] characters)
    {
        public TestCombatStateAdapter State { get; } = new(characters);
        public RecordingSnapshotBuilder Snapshots { get; } = new();
        public TestPipelineFactory Pipelines { get; } = new();
        public List<ScriptedDice> Dice { get; } = [];
        public Func<IDiceRoller>? DiceOverride { get; set; }
        public ICombatDiagnostics? Diagnostics { get; set; }
        private CombatImpactService? _service;
        public CombatImpactService Service => _service ??= new(State, Snapshots, Pipelines, () =>
        {
            if (DiceOverride is not null) return DiceOverride();
            var dice = new ScriptedDice(15, 3);
            Dice.Add(dice);
            return dice;
        }, Diagnostics);
    }

    private sealed class RecordingSnapshotBuilder : ICombatSnapshotBuilder
    {
        public List<CombatStateRead> Reads { get; } = [];
        public Func<CombatStateRead, CombatStateSnapshot>? Override { get; set; }
        public CombatStateSnapshot Build(CombatStateRead state)
        {
            Reads.Add(state);
            return Override?.Invoke(state) ?? new CombatSnapshotBuilder().Build(state);
        }
    }

    private sealed class TestPipelineFactory : ICombatPipelineFactory
    {
        public bool FailAfterProposals { get; set; }
        public bool HealSource { get; set; }
        public ICombatStep Create(AttackInstance attack, CombatStateSnapshot snapshot)
        {
            // Test content only: inspect CURRENT source effects on every call; no production
            // equipment aggregation or universal modifier stacking policy is defined here.
            var bonus = snapshot.Source.Effects.Select(effect => effect.Definition).OfType<BonusDefinition>().Sum(effect => effect.Bonus);
            var steps = new List<ICombatStep>
            {
                new AttackRollStep(new AttackRollResolver()), new AttackOutcomeStep(new D20AttackOutcomePolicy()),
                new RawDamageStep(new RawDamageResolver())
            };
            if (bonus != 0) steps.Add(new ApplyEffectsStep([new BonusDamageEffect(bonus)]));
            steps.Add(new AttackQualityStep(new AttackQualityDamageResolver(new DamageMultipliers(0, 0.5m, 1, 2))));
            steps.Add(new DefenseStep(new DefenseResolver(new HitMagnitudeArmorFormula())));
            if (HealSource) steps.Add(new ApplyEffectsStep([new SourceHealingEffect()]));
            steps.Add(new ApplyNewEffectsStep(new CombatEffectResolver(new ResolutionEffectInstanceIdFactory())));
            if (FailAfterProposals) steps.Add(new ApplyEffectsStep([new FailEffect()]));
            return new CombatPipeline(steps);
        }
    }

    private sealed class BonusDamageEffect(int bonus) : ICombatEffect
    {
        public CombatContext Apply(CombatContext context, IDiceRoller dice)
        {
            var previous = context.BeforeDefenses!;
            var packet = new DamagePacket(previous.Damage.Components
                .Select(value => new DamageAmount(value.Type, checked(value.Amount + bonus))).ToArray());
            var breakdown = new DamageBreakdown(previous.Breakdown.Steps.Append(
                new DamageStep(DamageStepType.AttackerModifier, previous.Damage, packet, origin: nameof(BonusDamageEffect), flatBonus: bonus)).ToArray());
            return context with { BeforeDefenses = new DamageCalculationResult(breakdown, packet) };
        }
    }

    private sealed class FailEffect : ICombatEffect
    {
        public CombatContext Apply(CombatContext context, IDiceRoller dice)
        {
            Assert.NotNull(context.BeforeDefenses);
            Assert.Single(context.StateChanges.OfType<EffectApplied>());
            throw new InvalidOperationException("Deliberate pipeline failure after proposals.");
        }
    }

    private sealed class SourceHealingEffect : ICombatEffect
    {
        public CombatContext Apply(CombatContext context, IDiceRoller dice) => context with
        {
            HealthAdjustments = context.HealthAdjustments.Add(new HealthAdjustment(context.Request.SourceId, 3))
        };
    }

    private sealed class ScriptedDice(params int[] values) : IDiceRoller
    {
        private readonly List<DiceRoll> _rolls = [];
        private readonly Queue<int> _values = new(values);
        public DiceTrace Trace => new(_rolls);
        public DiceRoll Roll(DiceFormula formula)
        {
            var values = Enumerable.Range(0, formula.Count).Select(_ => _values.Dequeue()).ToArray();
            var roll = new DiceRoll(formula, values, values.Sum());
            _rolls.Add(roll);
            return roll;
        }
    }
}
