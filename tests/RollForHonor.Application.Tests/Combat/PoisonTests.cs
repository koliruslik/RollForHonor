using RollForHonor.Application.Combat.Models;
using RollForHonor.Application.Combat.Ports;
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
using RollForHonor.Domain.Dice.Models;
using RollForHonor.Domain.Dice.Services;
using RollForHonor.Infrastructure.Combat;

namespace RollForHonor.Application.Tests.Combat;

/// <summary>Exercises poison through production impact composition, atomic state and independent scheduled ticks.</summary>
public sealed class PoisonTests
{
    /// <summary>Only successful hits consume a chance roll and the threshold is inclusive.</summary>
    /// <param name="face">Natural attack d20.</param>
    /// <param name="chanceRoll">Poison d100, if admitted.</param>
    /// <param name="stacks">Expected applied stack count.</param>
    /// <param name="rolls">Expected complete impact trace length.</param>
    [Theory]
    [InlineData(1, 1, 0, 2)]
    [InlineData(15, 10, 1, 4)]
    [InlineData(15, 11, 0, 4)]
    public void HitChanceUsesActualImpactOutcome(int face, int chanceRoll, int stacks, int rolls)
    {
        var rig = new Rig();
        var impact = rig.Hit(face, chanceRoll);

        Assert.True(impact.IsCommitted);
        var resolution = Assert.IsType<ResolvedCombat>(impact.Outcome).Resolution;
        Assert.Equal(stacks, rig.Target().Effects.Count);
        Assert.Equal(rolls, resolution.DiceTrace.Rolls.Count);
        Assert.Equal(stacks, resolution.Changes.OfType<EffectApplied>().Count());
    }

    /// <summary>Reapplication retains existing deadlines, source changes cannot alter stacks, and duplicate observation is harmless.</summary>
    [Fact]
    public void StacksExpireIndependentlyAndKeepCapturedDice()
    {
        var rig = new Rig();
        var first = rig.Apply(TimeSpan.Zero);
        rig.Scheduler.Observe(first, TimeSpan.Zero);
        Assert.Empty(rig.Scheduler.Advance(TimeSpan.FromMilliseconds(999)));
        Assert.Single(rig.Scheduler.Advance(TimeSpan.FromSeconds(1)));
        rig.SetSource(3);
        rig.Apply(TimeSpan.FromMilliseconds(1500));
        Assert.Equal(2, rig.Target().Effects.Count);
        var before = rig.Target().Health;

        var ticks = rig.Scheduler.Advance(TimeSpan.FromSeconds(5));

        Assert.Equal(7, ticks.Count);
        Assert.Equal(before - 13, rig.Target().Health);
        var remaining = Assert.Single(rig.Target().Effects);
        Assert.Equal(3, Assert.IsType<PeriodicDamageEffectDefinition>(remaining.Definition).Damage.Single().Dice.Count);
        Assert.Single(rig.Scheduler.Advance(TimeSpan.FromMilliseconds(5500)));
        Assert.Single(rig.Scheduler.Advance(TimeSpan.FromMilliseconds(6500)));
        Assert.Empty(rig.Target().Effects);
        Assert.Empty(rig.Scheduler.Advance(TimeSpan.FromSeconds(50)));
    }

    /// <summary>Poison ignores source multipliers, armor and even explicitly supplied pure resistance.</summary>
    [Fact]
    public void PureTicksIgnoreDefensesAndSourceDeath()
    {
        var rig = new Rig();
        rig.SetSource(2, 10);
        rig.Apply(TimeSpan.Zero);
        var source = rig.Source();
        rig.State.UpdateCombatant(new(source.Id, 0, source.MaxHealth, 0, 0, [], [], source.Offense));
        var before = rig.Target().Health;

        var ticks = rig.Scheduler.Advance(TimeSpan.FromSeconds(5));

        Assert.Equal(5, ticks.Count);
        Assert.Equal(before - 10, rig.Target().Health);
        Assert.All(ticks, tick =>
        {
            Assert.True(tick.IsCommitted);
            var result = Assert.IsType<ResolvedCombat>(tick.Outcome).Resolution;
            Assert.Null(result.Result.Target.AttackRoll);
            var roll = Assert.Single(result.DiceTrace.Rolls);
            Assert.Equal(new DiceFormula(2, 4), roll.Formula);
            Assert.Equal(2, result.Result.Target.DamageResolution!.AfterDefenses.Sum());
        });
        Assert.Empty(rig.Target().Effects);
    }

    /// <summary>Equal game times never rerun ticks and backwards clocks are rejected.</summary>
    [Fact]
    public void PausedClockDoesNotDuplicateTicks()
    {
        var rig = new Rig();
        rig.Apply(TimeSpan.Zero);
        Assert.Single(rig.Scheduler.Advance(TimeSpan.FromSeconds(1)));
        var health = rig.Target().Health;

        Assert.Empty(rig.Scheduler.Advance(TimeSpan.FromSeconds(1)));
        Assert.Equal(health, rig.Target().Health);
        Assert.Throws<ArgumentOutOfRangeException>(() => rig.Scheduler.Advance(TimeSpan.Zero));
    }

    /// <summary>A lethal tick stops all remaining stacks and emits one defeat without revival or additional rolls.</summary>
    [Fact]
    public void DefeatStopsPendingTicksAndRemovesStatuses()
    {
        var rig = new Rig();
        rig.Apply(TimeSpan.Zero);
        var target = rig.Target();
        rig.State.UpdateCombatant(new(target.Id, 1, target.MaxHealth, target.Armor, target.Evasion, target.Resistances, target.Effects));

        var tick = Assert.Single(rig.Scheduler.Advance(TimeSpan.FromSeconds(10)));

        Assert.Single(Assert.IsType<ResolvedCombat>(tick.Outcome).Resolution.Changes.OfType<CombatantDefeated>());
        Assert.True(rig.Target().IsDefeated);
        Assert.Empty(rig.Target().Effects);
        Assert.Empty(rig.Scheduler.Advance(TimeSpan.FromSeconds(20)));
    }

    /// <summary>Repeated applications retain more than ordinary small stack caps.</summary>
    [Fact]
    public void ApplicationsHaveNoStackCountCap()
    {
        var rig = new Rig();

        for (var index = 0; index < 64; index++)
        {
            rig.Apply(TimeSpan.Zero);
        }

        Assert.Equal(64, rig.Target().Effects.Count);
        Assert.Equal(64, rig.Target().Effects
            .Select(effect => effect.Id)
            .Distinct()
            .Count());
    }

    /// <summary>A conflicted slot is consumed once and expiry conflicts retry removal without rolling extra damage.</summary>
    [Fact]
    public void ConflictsCannotRepeatDamageAndExpiryCanRecover()
    {
        var rig = new Rig();
        var resolution = rig.Apply(TimeSpan.Zero);
        var adapter = new ConflictedState(rig.State);
        var ticks = new PeriodicEffectService(adapter,
            new CombatPipeline([new RawDamageStep(new RawDamageResolver()), new StatusDamageStep(), new SourceDamageStep(),
                new DefenseStep(new DefenseResolver(new RatingArmorFormula()))]), () => new ScriptedDice());
        var scheduler = new PeriodicEffectScheduler(adapter, ticks);
        scheduler.Observe(resolution, TimeSpan.Zero);
        var before = rig.Target().Health;

        var attempts = scheduler.Advance(TimeSpan.FromSeconds(5));

        Assert.Equal(5, attempts.Count);
        Assert.Equal(CombatCommitStatus.VersionConflict, attempts[0].CommitStatus);
        Assert.Equal(4, attempts.Count(tick => tick.IsCommitted));
        Assert.Equal(before - 4, rig.Target().Health);
        Assert.Single(rig.Target().Effects);
        Assert.Empty(scheduler.Advance(TimeSpan.FromSeconds(5)));
        Assert.Empty(rig.Target().Effects);
        Assert.Equal(before - 4, rig.Target().Health);
    }

    private sealed class ConflictedState(InMemoryCombatState state) : ICombatStateAdapter
    {
        private bool _rejectTick = true;
        private bool _rejectExpiry = true;

        /// <inheritdoc />
        public CombatStateRead Read(CombatantId sourceId, CombatantId targetId) => state.Read(sourceId, targetId);

        /// <inheritdoc />
        public CombatCommitStatus TryCommit(CombatResolution resolution)
        {
            if (_rejectTick && resolution.Changes.Any(change => change is HealthChanged))
            {
                _rejectTick = false;
                return CombatCommitStatus.VersionConflict;
            }

            if (_rejectExpiry && resolution.Changes.Any(change => change is EffectRemoved))
            {
                _rejectExpiry = false;
                return CombatCommitStatus.VersionConflict;
            }

            return state.TryCommit(resolution);
        }
    }

    private sealed class Rig
    {
        internal InMemoryCombatState State { get; } = new();
        internal PeriodicEffectScheduler Scheduler { get; }
        private readonly CombatantId _source = new(Guid.NewGuid());
        private readonly CombatantId _target = new(Guid.NewGuid());

        internal Rig()
        {
            State.AddCombatant(new(_source, 100, 100, 0, 0, [], []));
            State.AddCombatant(new(_target, 10000, 10000, 1000000, 0,
                [new(DamageType.Pure, 1), new(DamageType.Fire, 1)], []));
            SetSource(1);
            var ticks = new PeriodicEffectService(State,
                new CombatPipeline([new RawDamageStep(new RawDamageResolver()), new StatusDamageStep(), new SourceDamageStep(),
                    new DefenseStep(new DefenseResolver(new RatingArmorFormula()))]), () => new ScriptedDice());
            Scheduler = new(State, ticks);
        }

        internal CombatantSnapshot Source() => State.Read(_source, _source).Source;
        internal CombatantSnapshot Target() => State.Read(_target, _target).Target;

        internal void SetSource(int count, decimal multiplier = 1)
        {
            State.UpdateCombatant(new(_source, 100, 100, 0, 0, [], [],
                new(multipliers: [new(DamageType.Pure, multiplier)], poisonChance: 0.1m, poisonDice: count)));
        }

        internal CombatImpactResult Hit(int face = 15, int chance = 1)
        {
            var impacts = new CombatImpactService(State, new CombatSnapshotBuilder(),
                new BasicAttackPipelineFactory(new DamageMultipliers(0, 0.5m, 1, 2), ratingRules: true, statusRules: [new(StatusEffectKind.Poison, StatusEffectIds.Poison,
                    4, 0, DamageType.Pure, TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(1), 1, 100, false, false, 0)]),
                () => new ScriptedDice(face, 1, 1, chance));
            var payload = new AttackPayload(new(1, 20), 0, NaturalOutcomeShiftRules.Standard,
                [new(new(1, 4), 0, DamageType.Fire)], [], AttackTags.Projectile);
            return new AttackInstance(_source, payload, impacts).Resolve(_target);
        }

        internal CombatResolution Apply(TimeSpan at)
        {
            var impact = Hit();
            Assert.True(impact.IsCommitted);
            var resolution = Assert.IsType<ResolvedCombat>(impact.Outcome).Resolution;
            Scheduler.Observe(resolution, at);
            return resolution;
        }
    }

    private sealed class ScriptedDice(params int[] values) : IDiceRoller
    {
        private readonly Queue<int> _values = new(values);
        private readonly List<DiceRoll> _trace = [];

        /// <inheritdoc />
        public DiceTrace Trace => new(_trace);

        /// <inheritdoc />
        public DiceRoll Roll(DiceFormula formula)
        {
            var results = Enumerable.Range(0, formula.Count)
                .Select(_ => _values.Count > 0 ? _values.Dequeue() : 1)
                .ToArray();
            var roll = new DiceRoll(formula, results, results.Sum());
            _trace.Add(roll);
            return roll;
        }
    }
}
