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
using RollForHonor.Domain.Dice.Models;
using RollForHonor.Domain.Dice.Services;
using RollForHonor.Domain.Stats;
using RollForHonor.Infrastructure.Combat;

namespace RollForHonor.Application.Tests.Combat;

/// <summary>Verifies configurable status timing, cadence normalization, captured modifiers and duration-only reapplication.</summary>
public sealed class StatusEffectTests
{
    /// <summary>Guaranteed application remains certain on both slow and fast weapons and disappears when its modifier is removed.</summary>
    /// <param name="kind">The production status category.</param>
    /// <param name="speed">The accepted attack frequency.</param>
    [Theory]
    [InlineData(StatusEffectKind.Poison, 0.25)]
    [InlineData(StatusEffectKind.Poison, 100)]
    [InlineData(StatusEffectKind.Ignite, 0.25)]
    [InlineData(StatusEffectKind.Ignite, 100)]
    [InlineData(StatusEffectKind.Bleed, 0.25)]
    [InlineData(StatusEffectKind.Bleed, 100)]
    public void GuaranteedStatusOverridesCadenceOnlyWhilePresent(StatusEffectKind kind, decimal speed)
    {
        var rig = new Rig(kind);
        rig.SetSource(1, 0, [new(new(rig.Rule.Stats.Guaranteed), StatOperation.Flat, 1, "admin:T0")]);

        Assert.Equal(1, rig.Rule.Chance(rig.Source().Offense, speed));
        Assert.Single(rig.Apply(TimeSpan.Zero, speed, 1000000).Changes.OfType<EffectApplied>());

        rig.SetSource(1, 0);
        Assert.Equal(0, rig.Rule.Chance(rig.Source().Offense, speed));
        var result = rig.Apply(TimeSpan.FromMilliseconds(100), speed, 1);
        Assert.Empty(result.Changes.OfType<EffectApplied>());
        Assert.Empty(result.Changes.OfType<EffectDurationRefreshed>());
    }

    /// <summary>All three status kinds use the same inclusive normalized probability threshold.</summary>
    /// <param name="kind">The production status category.</param>
    /// <param name="speed">Actual attacks per second.</param>
    /// <param name="boundary">The last successful face on the configured probability die.</param>
    [Theory]
    [InlineData(StatusEffectKind.Poison, 0.5, 400000)]
    [InlineData(StatusEffectKind.Poison, 4, 50000)]
    [InlineData(StatusEffectKind.Ignite, 1, 200000)]
    [InlineData(StatusEffectKind.Ignite, 2, 100000)]
    [InlineData(StatusEffectKind.Bleed, 0.5, 400000)]
    [InlineData(StatusEffectKind.Bleed, 4, 50000)]
    public void StatusChanceNormalizesByAcceptedAttackCadence(StatusEffectKind kind, decimal speed, int boundary)
    {
        var rig = new Rig(kind);
        rig.SetSource(1, 0.2m);

        var success = rig.Apply(TimeSpan.Zero, speed, boundary);
        var failure = rig.Apply(TimeSpan.Zero, speed, boundary + 1);

        Assert.Single(success.Changes.OfType<EffectApplied>());
        Assert.Empty(failure.Changes.OfType<EffectApplied>());
        Assert.Empty(failure.Changes.OfType<EffectDurationRefreshed>());
        Assert.Single(rig.Target().Effects);
    }

    /// <summary>Probability is capped after normalization, so source chance modifiers remain useful on fast weapons.</summary>
    [Fact]
    public void ChanceModifiersAreNotPrematurelyCapped()
    {
        var rig = new Rig(StatusEffectKind.Poison);
        rig.SetSource(1, 2);
        Assert.Equal(0.5m, rig.Rule.Chance(rig.Source().Offense, 4));
        Assert.Equal(1, rig.Rule.Chance(rig.Source().Offense, 0.5m));
    }

    /// <summary>Ignite uses captured global/fire bonuses and current fire resistance, while ignoring armor and criticals.</summary>
    [Fact]
    public void IgniteUsesCapturedBuffsAndFreshResistanceWithoutArmor()
    {
        var rig = new Rig(StatusEffectKind.Ignite);
        rig.SetSource(2, 1, [new(new(StatType.DamageMultiplier), StatOperation.Increased, 1, "buff:global"),
            new(new(StatType.DamageMultiplier, DamageType.Fire), StatOperation.Increased, 1, "buff:fire"),
            new(new(StatType.IgniteDamageMultiplier), StatOperation.Increased, 1, "talent:ignite")]);
        rig.Apply(TimeSpan.Zero);
        rig.SetSource(5, 1);
        var before = rig.Target().Health;

        var tick = Assert.Single(rig.Scheduler.Advance(TimeSpan.FromSeconds(1)));

        Assert.True(tick.IsCommitted);
        Assert.Equal(before - 12, rig.Target().Health);
        var result = Assert.IsType<ResolvedCombat>(tick.Outcome).Resolution;
        Assert.Equal(2, Assert.Single(result.DiceTrace.Rolls).Formula.Count);
        Assert.Null(result.Result.Target.AttackRoll);
        Assert.Equal(24, result.Result.Target.DamageResolution!.BeforeDefenses.Sum());
        var profile = rig.State.GetStatProfile(rig.TargetId);
        rig.State.UpdateStats(rig.TargetId, new(profile.BaseValues.With(new(StatType.Armor), 0)
            .With(new(StatType.Resistance, DamageType.Fire), 0.75m), profile.Modifiers));
        Assert.Single(rig.Scheduler.Advance(TimeSpan.FromSeconds(2)));
        Assert.Equal(before - 18, rig.Target().Health);
    }

    /// <summary>Refreshing a wound changes only expiry, retaining its identity, source, damage, slowing and next due tick.</summary>
    [Fact]
    public void BleedRefreshesTimeWithoutReplacingCapturedParametersOrStackingSlow()
    {
        var rig = new Rig(StatusEffectKind.Bleed);
        rig.SetSource(1, 1);
        rig.Apply(TimeSpan.Zero);
        var original = Assert.Single(rig.Target().Effects);
        Assert.Equal(9, rig.State.GetStats(rig.TargetId).Get(new(StatType.MovementSpeed)));
        Assert.Equal(4, rig.Scheduler.Advance(TimeSpan.FromSeconds(4)).Count);
        rig.SetSource(3, 1, [new(new(StatType.BleedSlowMultiplier), StatOperation.Increased, 1, "talent:slow"),
            new(new(StatType.BleedDurationMultiplier), StatOperation.Increased, 1, "talent:duration")]);

        var refreshed = rig.Apply(TimeSpan.FromMilliseconds(4500));

        Assert.Single(refreshed.Changes.OfType<EffectDurationRefreshed>());
        Assert.Empty(refreshed.Changes.OfType<EffectApplied>());
        Assert.Same(original, Assert.Single(rig.Target().Effects));
        Assert.Equal(9, rig.State.GetStats(rig.TargetId).Get(new(StatType.MovementSpeed)));
        Assert.Single(rig.Scheduler.Advance(TimeSpan.FromSeconds(5)));
        Assert.Equal(9, rig.Scheduler.Advance(TimeSpan.FromSeconds(14)).Count);
        Assert.Single(rig.Target().Effects);
        Assert.Empty(rig.Scheduler.Advance(TimeSpan.FromMilliseconds(14500)));
        Assert.Empty(rig.Target().Effects);
        Assert.Equal(10, rig.State.GetStats(rig.TargetId).Get(new(StatType.MovementSpeed)));

        rig.Apply(TimeSpan.FromSeconds(15));
        var next = Assert.IsType<PeriodicDamageEffectDefinition>(Assert.Single(rig.Target().Effects).Definition);
        Assert.Equal(TimeSpan.FromSeconds(20), next.Duration);
        Assert.Equal(0.2m, next.MovementSlow);
        Assert.Equal(8, rig.State.GetStats(rig.TargetId).Get(new(StatType.MovementSpeed)));
        Assert.Equal(3, next.Damage.Single().Dice.Count);
        Assert.Equal(-3, next.Damage.Single().FlatDamage);
    }

    /// <summary>Bleed can roll zero and bypasses all ordinary defenses and hit bonuses.</summary>
    /// <param name="face">Each damage die's face.</param>
    /// <param name="damage">Expected total damage from one three-die tick.</param>
    [Theory]
    [InlineData(1, 0)]
    [InlineData(2, 3)]
    [InlineData(4, 9)]
    public void BleedSubtractsOnePerDieAndCanDealZero(int face, int damage)
    {
        var rig = new Rig(StatusEffectKind.Bleed, tickFace: face);
        rig.SetSource(3, 1, [new(new(StatType.DamageMultiplier), StatOperation.Increased, 9, "buff:damage")]);
        rig.Apply(TimeSpan.Zero);
        var before = rig.Target().Health;

        var tick = Assert.Single(rig.Scheduler.Advance(TimeSpan.FromSeconds(1)));

        Assert.True(tick.IsCommitted);
        Assert.Equal(before - damage, rig.Target().Health);
        Assert.Equal(new DiceFormula(3, 4), Assert.Single(Assert.IsType<ResolvedCombat>(tick.Outcome).Resolution.DiceTrace.Rolls).Formula);
    }

    /// <summary>Ordinary stat modifiers change damage, lifetime and tick frequency without editing mechanics or refreshing old stacks.</summary>
    [Fact]
    public void TalentInputsModifyNewApplicationsAndFractionalLifetimeExpiresExactly()
    {
        var rig = new Rig(StatusEffectKind.Poison);
        rig.SetSource(1, 1, [new(new(StatType.PoisonDurationMultiplier), StatOperation.Increased, 0.5m, "talent:duration"),
            new(new(StatType.PoisonDamageMultiplier), StatOperation.More, 1, "talent:damage")]);
        rig.Apply(TimeSpan.Zero);
        var effect = Assert.IsType<PeriodicDamageEffectDefinition>(Assert.Single(rig.Target().Effects).Definition);
        Assert.Equal(TimeSpan.FromMilliseconds(7500), effect.Duration);
        Assert.Equal(7, effect.TickCount);
        var before = rig.Target().Health;

        Assert.Equal(7, rig.Scheduler.Advance(TimeSpan.FromMilliseconds(7500)).Count);
        Assert.Equal(before - 28, rig.Target().Health);
        Assert.Empty(rig.Target().Effects);
        rig.SetSource(1, 1, [new(new(StatType.PoisonTickRateMultiplier), StatOperation.Increased, 1, "talent:frequency")]);
        rig.Apply(TimeSpan.FromSeconds(8));
        Assert.Equal(10, rig.Scheduler.Advance(TimeSpan.FromSeconds(13)).Count);
        Assert.Empty(rig.Target().Effects);
    }

    /// <summary>A rule with different dice, timing and reference rate is honored by the same production steps.</summary>
    [Fact]
    public void AlternateContentRequiresNoCodeChange()
    {
        var rule = new OnHitStatusRule(StatusEffectKind.Poison, StatusEffectIds.Poison, 6, -1, DamageType.Pure,
            TimeSpan.FromSeconds(8), TimeSpan.FromSeconds(2), 2, 10000, false, false, 0);
        var rig = new Rig(StatusEffectKind.Poison, rule);
        rig.SetSource(2, 0.2m);
        Assert.Equal(0.4m, rule.Chance(rig.Source().Offense, 1));
        rig.Apply(TimeSpan.Zero);
        var before = rig.Target().Health;

        var ticks = rig.Scheduler.Advance(TimeSpan.FromSeconds(8));

        Assert.Equal(4, ticks.Count);
        Assert.Equal(before - 8, rig.Target().Health);
        Assert.All(ticks, tick => Assert.Equal(new DiceFormula(2, 6),
            Assert.Single(Assert.IsType<ResolvedCombat>(tick.Outcome).Resolution.DiceTrace.Rolls).Formula));
    }

    private sealed class Rig
    {
        internal InMemoryCombatState State { get; } = new();
        internal CombatantId TargetId { get; } = new(Guid.NewGuid());
        internal OnHitStatusRule Rule { get; }
        internal PeriodicEffectScheduler Scheduler { get; }
        private readonly CombatantId _source = new(Guid.NewGuid());

        internal Rig(StatusEffectKind kind, OnHitStatusRule? rule = null, int tickFace = 2)
        {
            Rule = rule ?? new(kind, kind == StatusEffectKind.Poison ? StatusEffectIds.Poison
                : kind == StatusEffectKind.Ignite ? StatusEffectIds.Ignite : StatusEffectIds.Bleed,
                4, kind == StatusEffectKind.Bleed ? -1 : 0, kind == StatusEffectKind.Ignite ? DamageType.Fire : DamageType.Pure,
                TimeSpan.FromSeconds(kind == StatusEffectKind.Bleed ? 10 : 5), TimeSpan.FromSeconds(1),
                1, 1000000, kind != StatusEffectKind.Poison, kind == StatusEffectKind.Ignite, kind == StatusEffectKind.Bleed ? 0.1m : 0);
            State.AddCombatant(new(_source, 100, 100, 0, 0, [], []));
            State.AddStatCombatant(TargetId, new(new StatBlock()
                .With(new(StatType.MaxHealth), 10000).With(new(StatType.MovementSpeed), 10)
                .With(new(StatType.Armor), 1000000).With(new(StatType.Resistance, DamageType.Fire), 0.5m)));
            SetSource(1, 1);
            var ticks = new PeriodicEffectService(State, new CombatPipeline([new RawDamageStep(new RawDamageResolver()),
                new StatusDamageStep(), new SourceDamageStep(), new DefenseStep(new DefenseResolver(new RatingArmorFormula()))]),
                () => new FixedDice(tickFace));
            Scheduler = new(State, ticks);
        }

        internal CombatantSnapshot Source() => State.Read(_source, _source).Source;
        internal CombatantSnapshot Target() => State.Read(TargetId, TargetId).Target;

        internal void SetSource(int dice, decimal chance, IEnumerable<StatModifier>? modifiers = null)
        {
            var stats = new StatCalculator().Calculate(new(new StatBlock()
                .With(new(Rule.Stats.Chance), chance).With(new(Rule.Stats.Dice), dice), modifiers));
            State.UpdateCombatant(new(_source, 100, 100, 0, 0, [], [],
                new(poisonChance: stats.Get(new(StatType.PoisonChance)), poisonDice: (int)stats.Get(new(StatType.PoisonDice)),
                    multipliers: [new(DamageType.Fire, stats.Get(new(StatType.DamageMultiplier, DamageType.Fire)))], statusStats: stats)));
        }

        internal CombatResolution Apply(TimeSpan at, decimal speed = 1, int chanceFace = 1)
        {
            var impacts = new CombatImpactService(State, new CombatSnapshotBuilder(),
                new BasicAttackPipelineFactory(new DamageMultipliers(0, 0.5m, 1, 2), true, [Rule]),
                () => new ScriptedDice(15, 1, 1, chanceFace));
            var payload = new AttackPayload(new(1, 20), 0, NaturalOutcomeShiftRules.Standard,
                [new(new(1, 4), -1, DamageType.Fire)], [], AttackTags.Projectile);
            var impact = new AttackInstance(_source, payload, impacts,
                attackInterval: TimeSpan.FromSeconds((double)(1 / speed))).Resolve(TargetId);
            Assert.True(impact.IsCommitted);
            var resolution = Assert.IsType<ResolvedCombat>(impact.Outcome).Resolution;
            Scheduler.Observe(resolution, at);
            return resolution;
        }
    }

    private sealed class FixedDice(int face) : IDiceRoller
    {
        private readonly List<DiceRoll> _rolls = [];

        /// <inheritdoc />
        public DiceTrace Trace => new(_rolls);

        /// <inheritdoc />
        public DiceRoll Roll(DiceFormula formula)
        {
            var results = Enumerable.Repeat(face, formula.Count).ToArray();
            var roll = new DiceRoll(formula, results, results.Sum());
            _rolls.Add(roll);
            return roll;
        }
    }

    private sealed class ScriptedDice(params int[] values) : IDiceRoller
    {
        private readonly Queue<int> _values = new(values);
        private readonly List<DiceRoll> _rolls = [];

        /// <inheritdoc />
        public DiceTrace Trace => new(_rolls);

        /// <inheritdoc />
        public DiceRoll Roll(DiceFormula formula)
        {
            var results = Enumerable.Range(0, formula.Count)
                .Select(_ => _values.Dequeue())
                .ToArray();
            var roll = new DiceRoll(formula, results, results.Sum());
            _rolls.Add(roll);
            return roll;
        }
    }
}
