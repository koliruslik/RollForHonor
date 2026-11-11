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
using RollForHonor.Domain.Combat.State.Services;
using RollForHonor.Domain.Dice.Models;
using RollForHonor.Domain.Dice.Services;
using RollForHonor.Domain.Tests.Diagnostics;
using Xunit.Abstractions;

namespace RollForHonor.Domain.Tests.Combat.Scenarios;

public sealed class MultiImpactScenarioTests(ITestOutputHelper output) : CombatDiagnosticTestBase(output)
{
    [Fact]
    public void OneAttack_ThreeTargets_ResolveIndependentDefensesAndCommitSeparateTransactions()
    {
        var source = Character();
        var targets = new[] { Character(), Character(armor: 30),
            Character(resistances: [new ResistanceValue(DamageType.Slashing, 0.5m)]) };
        var app = CreateApplication([source, .. targets]);
        var attack = app.BeginAttack(source.Id, Payload());
        var resolver = AttackResolver();
        for (var index = 0; index < targets.Length; index++)
        {
            var impact = app.CalculateImpact(attack, targets[index].Id, resolver, new ScriptedDice(15, 3));
            var resolution = Resolution(impact);
            Assert.Equal(index, resolution.BasedOnStateVersion);
            Assert.Equal(new[] { 6, 3, 3 }[index], resolution.Result.Target.DamageResolution!.AfterDefenses.Sum());
            Assert.Equal(50, app.Snapshot(targets[index].Id).Health); // calculation has not committed
            Assert.True(app.TryCommit(impact));
            Assert.Equal(new[] { 44, 47, 47 }[index], app.Snapshot(targets[index].Id).Health);
            Assert.Equal(50, app.Snapshot(source.Id).Health);
            Assert.All(targets.Skip(index + 1), target => Assert.Equal(50, app.Snapshot(target.Id).Health));
            Assert.All(resolution.Changes.OfType<HealthChanged>(), change => Assert.Equal(targets[index].Id, change.CombatantId));
            Assert.Equal(new[] { 20, 6 }, resolution.DiceTrace.Rolls.Select(roll => roll.Formula.Sides));
        }
        Assert.Single(app.Attacks);
        Assert.Equal(3, app.Version);
        Assert.Equal(3, app.Committed.Select(impact => impact.Request.ResolutionId).Distinct().Count());
        Assert.All(app.Committed, impact =>
        {
            Assert.Equal(attack.Id, impact.AttackId);
            Assert.Same(attack.Payload, impact.Request.Attack);
        });
        Assert.All(targets, target => Assert.Equal(50, target.Health));
    }

    [Fact]
    public void OneAttack_ThreeHitsOnSameTarget_UseCommittedHealthAndDefeatOnLastHit()
    {
        var source = Character();
        var target = Character(15);
        var app = CreateApplication(source, target);
        var attack = app.BeginAttack(source.Id, Payload());
        for (var index = 0; index < 3; index++)
        {
            var impact = app.CalculateImpact(attack, target.Id, AttackResolver(), new ScriptedDice(15, 3));
            Assert.Equal(new[] { 15, 9, 3 }[index], impact.Request.StateSnapshot.Target.Health);
            var health = Assert.Single(Resolution(impact).Changes.OfType<HealthChanged>());
            Assert.Equal(new[] { 9, 3, 0 }[index], health.CurrentHealth);
            Assert.Equal(index == 2, Resolution(impact).Changes.OfType<CombatantDefeated>().Any());
            Assert.True(app.TryCommit(impact));
            Assert.Equal(health.CurrentHealth, app.Snapshot(target.Id).Health);
        }
        Assert.True(app.Snapshot(target.Id).IsDefeated);
        Assert.Single(app.Committed.SelectMany(impact => Resolution(impact).Changes.OfType<CombatantDefeated>()));
        Assert.All(app.Committed, impact => Assert.Equal(attack.Id, impact.AttackId));
        Assert.Equal(15, target.Health);
    }

    [Fact]
    public void FirstImpactAppliesResistance_NextImpactReadsIt_ReplacementDoesNotAccumulateBonuses()
    {
        var source = Character();
        var target = Character();
        var app = CreateApplication(source, target);
        var definition = new TestResistanceDefinition(DamageType.Slashing, 0.5m);
        var attack = app.BeginAttack(source.Id, Payload(definition));
        var resolver = AttackResolver(applyStatuses: true);
        var first = app.CalculateImpact(attack, target.Id, resolver, new ScriptedDice(15, 3));
        Assert.Equal(6, Resolution(first).Result.Target.DamageResolution!.AfterDefenses.Sum());
        Assert.Empty(app.Snapshot(target.Id).Effects);
        Assert.True(app.TryCommit(first));
        var firstEffect = Assert.Single(app.Snapshot(target.Id).Effects);

        for (var index = 0; index < 2; index++)
        {
            var impact = app.CalculateImpact(attack, target.Id, resolver, new ScriptedDice(15, 3));
            var resolution = Resolution(impact);
            Assert.Equal(3, resolution.Result.Target.DamageResolution!.AfterDefenses.Sum());
            var old = Assert.Single(app.Snapshot(target.Id).Effects);
            Assert.Equal(old.Id, Assert.Single(resolution.Changes.OfType<EffectRemoved>()).EffectId);
            Assert.True(app.TryCommit(impact));
            Assert.NotEqual(old.Id, Assert.Single(app.Snapshot(target.Id).Effects).Id);
            Assert.Equal(0.5m, Assert.Single(app.Snapshot(target.Id).Resistances).Value);
        }
        Assert.Equal(38, app.Snapshot(target.Id).Health); // 50 - 6 - 3 - 3
        Assert.Equal(0.5m, Assert.Single(app.Snapshot(target.Id).Resistances).Value);
        Assert.Equal(50, firstEffect.SourceSnapshot!.Health);
        Assert.Empty(target.Effects);
        Assert.Empty(target.Resistances);
    }

    [Fact]
    public void RemovedResistanceStatus_NextHitReturnsToBaseDefense()
    {
        var source = Character();
        var target = Character();
        var app = CreateApplication(source, target);
        var applyingAttack = app.BeginAttack(source.Id, Payload(new TestResistanceDefinition(DamageType.Slashing, 0.5m)));
        var first = app.CalculateImpact(applyingAttack, target.Id, AttackResolver(applyStatuses: true), new ScriptedDice(15, 3));
        Assert.True(app.TryCommit(first));
        var instance = Assert.Single(app.Snapshot(target.Id).Effects);

        // Explicit fixture behavior removes the status after this hit's defenses.
        var followup = app.BeginAttack(source.Id, Payload());
        var remove = new ApplyEffectsStep([new RemoveStatus(instance.Id)]);
        var removing = app.CalculateImpact(followup, target.Id, AttackResolver(afterDefenses: remove), new ScriptedDice(15, 3));
        Assert.Equal(3, Resolution(removing).Result.Target.DamageResolution!.AfterDefenses.Sum());
        Assert.True(app.TryCommit(removing));
        Assert.Empty(app.Snapshot(target.Id).Effects);
        Assert.Empty(app.Snapshot(target.Id).Resistances);

        var next = app.CalculateImpact(followup, target.Id, AttackResolver(), new ScriptedDice(15, 3));
        Assert.Equal(6, Resolution(next).Result.Target.DamageResolution!.AfterDefenses.Sum());
        Assert.True(app.TryCommit(next));
        Assert.Equal(35, app.Snapshot(target.Id).Health); // 50 - 6 - 3 - 6
    }

    [Fact]
    public void TickBetweenHits_UsesAppliedEffectAndUpdatesHealthForNextImpact()
    {
        var source = Character();
        var target = Character();
        var app = CreateApplication(source, target);
        var attack = app.BeginAttack(source.Id, Payload(Burning()));
        var first = app.CalculateImpact(attack, target.Id, AttackResolver(applyStatuses: true), new ScriptedDice(15, 3));
        Assert.True(app.TryCommit(first));
        var effect = Assert.Single(app.Snapshot(target.Id).Effects);
        var tick = app.CalculateTick(effect, target.Id, TickResolver(), new ScriptedDice(1));
        Assert.Null(tick.Request.Attack);
        Assert.Null(Resolution(tick).Result.Target.AttackRoll);
        Assert.True(app.TryCommit(tick));
        Assert.Equal(40, app.Snapshot(target.Id).Health); // 50 - 6 - (1 + 3)

        var next = app.CalculateImpact(attack, target.Id, AttackResolver(applyStatuses: true), new ScriptedDice(15, 3));
        Assert.Equal(40, next.Request.StateSnapshot.Target.Health);
        Assert.True(app.TryCommit(next));
        Assert.Equal(34, app.Snapshot(target.Id).Health);
        Assert.Equal(new long[] { 0, 1, 2 }, app.Committed.Select(impact => Resolution(impact).BasedOnStateVersion));
        Assert.Equal(attack.Id, first.AttackId);
        Assert.Equal(attack.Id, next.AttackId);
        Assert.Null(tick.AttackId);
    }

    [Fact]
    public void TickAfterSourceWasActuallyKilled_UsesCapturedSourceAndCommitsOnlyRecipient()
    {
        var source = Character(6);
        var target = Character();
        var killer = Character();
        var app = CreateApplication(source, target, killer);
        var attack = app.BeginAttack(source.Id, Payload(Burning()));
        var first = app.CalculateImpact(attack, target.Id, AttackResolver(applyStatuses: true), new ScriptedDice(15, 3));
        Assert.True(app.TryCommit(first));
        var effect = Assert.Single(app.Snapshot(target.Id).Effects);

        var killingAttack = app.BeginAttack(killer.Id, Payload());
        var kill = app.CalculateImpact(killingAttack, source.Id, AttackResolver(), new ScriptedDice(15, 3));
        Assert.True(app.TryCommit(kill));
        Assert.True(app.Snapshot(source.Id).IsDefeated);

        var tick = app.CalculateTick(effect, target.Id, TickResolver(), new ScriptedDice(3));
        Assert.Equal(6, tick.Request.StateSnapshot.Source.Health);
        Assert.All(Resolution(tick).Changes.OfType<HealthChanged>(), change => Assert.Equal(target.Id, change.CombatantId));
        Assert.True(app.TryCommit(tick));
        Assert.Equal(38, app.Snapshot(target.Id).Health);
        Assert.Equal(0, app.Snapshot(source.Id).Health);
        Assert.Equal(6, effect.SourceSnapshot!.Health);
    }

    [Fact]
    public void StaleImpact_DoesNotPartiallyApplyHealthOrStatus_RecalculationUsesNewVersion()
    {
        var source = Character();
        var target = Character();
        var other = Character();
        var app = CreateApplication(source, target, other);
        var attack = app.BeginAttack(source.Id, Payload(Burning()));
        var resolver = AttackResolver(applyStatuses: true);
        var stale = app.CalculateImpact(attack, target.Id, resolver, new ScriptedDice(15, 3));
        var preceding = app.CalculateImpact(attack, other.Id, resolver, new ScriptedDice(15, 3));
        Assert.True(app.TryCommit(preceding));
        Assert.False(app.TryCommit(stale));
        Assert.Equal(50, app.Snapshot(target.Id).Health);
        Assert.Empty(app.Snapshot(target.Id).Effects);
        Assert.Equal(44, app.Snapshot(other.Id).Health);
        Assert.Single(app.Committed);

        var fresh = app.CalculateImpact(attack, target.Id, resolver, new ScriptedDice(15, 3));
        Assert.Equal(1, fresh.Request.StateSnapshot.Version);
        Assert.NotEqual(stale.Request.ResolutionId, fresh.Request.ResolutionId);
        Assert.True(app.TryCommit(fresh));
        Assert.Equal(44, app.Snapshot(target.Id).Health);
        Assert.Single(app.Snapshot(target.Id).Effects);
        Assert.Equal(2, app.Version);
    }

    [Fact]
    public void DuplicateCommit_DoesNotRepeatDamageStatusOrVersionIncrement()
    {
        var source = Character();
        var target = Character();
        var app = CreateApplication(source, target);
        var attack = app.BeginAttack(source.Id, Payload(Burning()));
        var impact = app.CalculateImpact(attack, target.Id, AttackResolver(applyStatuses: true), new ScriptedDice(15, 3));
        Assert.True(app.TryCommit(impact));
        var effect = Assert.Single(app.Snapshot(target.Id).Effects);
        Assert.False(app.TryCommit(impact));
        Assert.Equal(44, app.Snapshot(target.Id).Health);
        Assert.Same(effect, Assert.Single(app.Snapshot(target.Id).Effects));
        Assert.Equal(1, app.Version);
        Assert.Single(app.Committed);
    }

    [Fact]
    public void RejectedImpact_DoesNotCommitOrConsumeDice_AndOtherAttackCanStillResolve()
    {
        var dead = Character(0);
        var alive = Character();
        var target = Character();
        var app = CreateApplication(dead, alive, target);
        var invalid = app.BeginAttack(dead.Id, Payload(Burning()));
        var dice = new ScriptedDice();
        var rejected = app.CalculateImpact(invalid, target.Id, AttackResolver(applyStatuses: true), dice);
        Assert.Equal(CombatRejectionReason.SourceDefeated, Assert.IsType<RejectedCombat>(rejected.Outcome).Rejection.Reason);
        Assert.Empty(dice.Trace.Rolls);
        Assert.False(app.TryCommit(rejected));
        Assert.Equal(0, app.Version);
        Assert.Equal(50, app.Snapshot(target.Id).Health);
        Assert.Empty(app.Snapshot(target.Id).Effects);

        var valid = app.BeginAttack(alive.Id, Payload());
        var result = app.CalculateImpact(valid, target.Id, AttackResolver(), new ScriptedDice(15, 3));
        Assert.True(app.TryCommit(result));
        Assert.Equal(44, app.Snapshot(target.Id).Health);
        Assert.Single(app.Committed);
    }

    [Fact]
    public void PipelineFailureAfterDamageAndStatusProposals_DoesNotPublishPartialState()
    {
        var source = Character();
        var target = Character();
        var app = CreateApplication(source, target);
        var attack = app.BeginAttack(source.Id, Payload(Burning()));
        var resolver = Resolver([
            new RawDamageStep(new RawDamageResolver()), StatusApplication(), new ApplyEffectsStep([new FailAfterProposals()])
        ]);
        var dice = new ScriptedDice(3);
        Assert.Throws<InvalidOperationException>(() => app.CalculateImpact(attack, target.Id, resolver, dice));
        Assert.Single(dice.Trace.Rolls);
        Assert.Equal(0, app.Version);
        Assert.Empty(app.Committed);
        Assert.Equal(50, app.Snapshot(source.Id).Health);
        Assert.Equal(50, app.Snapshot(target.Id).Health);
        Assert.Empty(app.Snapshot(target.Id).Effects);
    }

    [Fact]
    public void OneImpactChangesBothParticipants_CommitPublishesBothAtOneVersion()
    {
        var source = Character(20);
        var target = Character();
        var app = CreateApplication(source, target);
        var attack = app.BeginAttack(source.Id, Payload());
        var impact = app.CalculateImpact(attack, target.Id,
            AttackResolver(afterDefenses: new ApplyEffectsStep([new HealSource(3)])), new ScriptedDice(15, 3));
        Assert.Equal(2, Resolution(impact).Changes.OfType<HealthChanged>().Count());
        Assert.Equal(20, app.Snapshot(source.Id).Health);
        Assert.Equal(50, app.Snapshot(target.Id).Health);
        Assert.True(app.TryCommit(impact));
        Assert.Equal(23, app.Snapshot(source.Id).Health);
        Assert.Equal(44, app.Snapshot(target.Id).Health);
        Assert.Equal(1, app.Version);
    }

    private TestCombatApplication CreateApplication(params CombatantSnapshot[] characters) =>
        new(outcome => LogOutcome(outcome), characters);

    private static CombatantSnapshot Character(int health = 50, int armor = 0,
        IReadOnlyList<ResistanceValue>? resistances = null) =>
        new(new CombatantId(Guid.NewGuid()), health, 100, armor, 15, resistances ?? [], []);

    private static AttackPayload Payload(params CombatEffectDefinition[] effects) =>
        new(new DiceFormula(1, 20), 0, NaturalOutcomeShiftRules.Standard,
            [new DamageComponent(new DiceFormula(1, 6), 3, DamageType.Slashing)], effects, AttackTags.Projectile);

    private static PeriodicDamageEffectDefinition Burning() =>
        new(new EffectDefinitionId(Guid.NewGuid()), [new DamageComponent(new DiceFormula(1, 6), 3, DamageType.Fire)]);

    private static CombatResolution Resolution(TestImpact impact) => Assert.IsType<ResolvedCombat>(impact.Outcome).Resolution;

    private static CombatResolver AttackResolver(bool applyStatuses = false, ICombatStep? afterDefenses = null)
    {
        var steps = new List<ICombatStep>
        {
            new AttackRollStep(new AttackRollResolver()), new AttackOutcomeStep(new D20AttackOutcomePolicy()),
            new RawDamageStep(new RawDamageResolver()),
            new AttackQualityStep(new AttackQualityDamageResolver(new DamageMultipliers(0, 0.5m, 1, 2))),
            new DefenseStep(new DefenseResolver(new HitMagnitudeArmorFormula()))
        };
        if (afterDefenses is not null) steps.Add(afterDefenses);
        if (applyStatuses) steps.Add(StatusApplication());
        return Resolver(steps);
    }

    private static CombatResolver TickResolver() => Resolver([
        new RawDamageStep(new RawDamageResolver()), new DefenseStep(new DefenseResolver(new HitMagnitudeArmorFormula()))
    ]);

    private static ApplyNewEffectsStep StatusApplication() =>
        new(new CombatEffectResolver(new ResolutionEffectInstanceIdFactory()));

    private static CombatResolver Resolver(IEnumerable<ICombatStep> steps) =>
        new(new CombatRequestValidator(), new CombatPipeline(steps), new CombatResolutionBuilder(new HealthStateProjector()));

    private sealed class ScriptedDice(params int[] values) : IDiceRoller
    {
        private readonly Queue<int> _values = new(values);
        private readonly List<DiceRoll> _rolls = [];
        public DiceTrace Trace => new(_rolls);
        public DiceRoll Roll(DiceFormula formula)
        {
            var results = Enumerable.Range(0, formula.Count).Select(_ => _values.Dequeue()).ToArray();
            var roll = new DiceRoll(formula, results, results.Sum());
            _rolls.Add(roll);
            return roll;
        }
    }

    private sealed class RemoveStatus(EffectInstanceId id) : ICombatEffect
    {
        public CombatContext Apply(CombatContext context, IDiceRoller dice) => context with
        {
            StateChanges = context.StateChanges.Add(new EffectRemoved(context.Request.TargetId, id))
        };
    }

    private sealed class HealSource(int amount) : ICombatEffect
    {
        public CombatContext Apply(CombatContext context, IDiceRoller dice) => context with
        {
            HealthAdjustments = context.HealthAdjustments.Add(new HealthAdjustment(context.Request.SourceId, amount))
        };
    }

    private sealed class FailAfterProposals : ICombatEffect
    {
        public CombatContext Apply(CombatContext context, IDiceRoller dice)
        {
            Assert.NotNull(context.BeforeDefenses);
            Assert.Single(context.StateChanges.OfType<EffectApplied>());
            throw new InvalidOperationException("Deliberate scenario failure after proposals were calculated.");
        }
    }
}
