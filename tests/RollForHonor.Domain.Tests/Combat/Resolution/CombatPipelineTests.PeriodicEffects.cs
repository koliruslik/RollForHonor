using RollForHonor.Domain.Combat.Attacks.Models;
using RollForHonor.Domain.Combat.Attacks.Services;
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

namespace RollForHonor.Domain.Tests.Combat.Resolution;

public sealed partial class CombatPipelineTests
{
    [Theory]
    [InlineData(1, AttackOutcome.Evaded, 0)]
    [InlineData(6, AttackOutcome.GlancingHit, 1)]
    [InlineData(15, AttackOutcome.Hit, 3)]
    [InlineData(20, AttackOutcome.CriticalHit, 8)]
    public void ProductionAttack_AllOutcomesUseRealStepsAndExplicitExpectedDamage(
        int attackRoll, AttackOutcome outcome, int expectedDamage)
    {
        var source = Combatant(50);
        var target = new CombatantSnapshot(new CombatantId(Guid.NewGuid()), 50, 100, 30, 15, [], []);
        var payload = new AttackPayload(new DiceFormula(1, 20), 0, NaturalOutcomeShiftRules.Standard,
            [new DamageComponent(new DiceFormula(1, 6), 3, DamageType.Slashing)], [], AttackTags.Melee);
        var request = new CombatRequest(Guid.NewGuid(), source.Id, target.Id, payload,
            new CombatStateSnapshot(5, source, target));
        var result = Resolve(Resolver(IndependentAttackSteps().Append(
            new DefenseStep(new DefenseResolver(new HitMagnitudeArmorFormula())))), request,
            new ScriptedDice(attackRoll, 3));

        Assert.Equal(outcome, result.Result.Target.Outcome);
        Assert.Equal(expectedDamage, result.Result.Target.DamageResolution!.AfterDefenses.Sum());
        Assert.Equal(5, result.BasedOnStateVersion);
        Assert.Equal(new[] { 20, 6 }, result.DiceTrace.Rolls.Select(roll => roll.Formula.Sides));
        if (expectedDamage == 0) Assert.Empty(result.Changes);
        else Assert.Equal(50 - expectedDamage, Assert.Single(result.Changes.OfType<HealthChanged>()).CurrentHealth);
    }

    [Fact]
    public void ConsecutiveAttacks_UseUpdatedHealthAndVersion_AndDefeatOnlyOnce()
    {
        var source = Combatant(50);
        var target = Combatant(25);
        var resolver = Resolver([RawDamage()]);
        for (var version = 0; version < 3; version++)
        {
            var ordinary = CreateRequest(source, target);
            var request = new CombatRequest(Guid.NewGuid(), source.Id, target.Id, ordinary.Attack!,
                new CombatStateSnapshot(version, source, target));
            var result = Resolve(resolver, request, new CountingDice());
            Assert.Equal(version, result.BasedOnStateVersion);
            var change = Assert.Single(result.Changes.OfType<HealthChanged>());
            Assert.Equal(target.Health, change.PreviousHealth);
            Assert.Equal(new[] { 15, 5, 0 }[version], change.CurrentHealth);
            Assert.Equal(version == 2, result.Changes.OfType<CombatantDefeated>().Any());
            target = WithState(target, change.CurrentHealth);
        }
        Assert.Equal(50, source.Health);
    }

    [Fact]
    public void AppliedEffect_CapturesSource_AndRerollsAgainstFreshRecipientAfterSourceDeath()
    {
        var source = Combatant(50, armor: 19);
        var target = Combatant(50);
        var definition = Periodic();
        var applied = Resolve(Resolver([StatusApplication()]),
            CreateRequest(source, target, effects: [definition], damage: []), new ScriptedDice());
        var instance = Assert.Single(applied.Changes.OfType<EffectApplied>()).Effect;
        Assert.Same(source, instance.SourceSnapshot);
        target = WithState(target, 50, effects: [instance]);

        // Live source state is no longer passed to the resolver, even after death/stat changes.
        var liveSource = WithState(source, 0, armor: 999);
        var firstRequest = new CombatRequest(Guid.NewGuid(), instance, target, 2);
        Assert.Null(firstRequest.Attack);
        var first = Resolve(Resolver([RawDamage()]), firstRequest, new ScriptedDice(1));
        Assert.Equal(46, Assert.Single(first.Changes.OfType<HealthChanged>()).CurrentHealth);
        Assert.Equal(source.Id, first.Result.SourceId);
        Assert.Null(first.Result.Target.AttackRoll);
        Assert.Null(first.Result.Target.Outcome);
        Assert.Equal(6, Assert.Single(first.DiceTrace.Rolls).Formula.Sides);

        // This scenario explicitly includes typed defenses; it is not a universal tick policy.
        target = WithState(target, 46, resistances: [new ResistanceValue(DamageType.Fire, 0.5m)]);
        var second = Resolve(Resolver([RawDamage(), new DefenseStep(new DefenseResolver(new HitMagnitudeArmorFormula()))]),
            new CombatRequest(Guid.NewGuid(), instance, target, 3), new ScriptedDice(6));
        Assert.Equal(9, second.Result.Target.DamageResolution!.BeforeDefenses.Sum());
        Assert.Equal(5, second.Result.Target.DamageResolution.AfterDefenses.Sum());
        Assert.Equal(41, Assert.Single(second.Changes.OfType<HealthChanged>()).CurrentHealth);
        Assert.Equal(3, second.BasedOnStateVersion);
        Assert.Same(source, instance.SourceSnapshot);
        Assert.Equal(50, instance.SourceSnapshot!.Health);
        Assert.Equal(19, instance.SourceSnapshot.Armor);
        Assert.True(liveSource.IsDefeated);
        Assert.All(second.Changes.OfType<HealthChanged>(), change => Assert.Equal(target.Id, change.CombatantId));
    }

    [Fact]
    public void SelfAppliedEffect_UsesCurrentTargetHealthInsteadOfCapturedHealth()
    {
        var character = Combatant(50);
        var applied = Resolve(Resolver([StatusApplication()]),
            CreateRequest(character, character, [Periodic(EffectRecipient.Source)], []), new ScriptedDice());
        var instance = Assert.Single(applied.Changes.OfType<EffectApplied>()).Effect;
        var current = WithState(character, 20, effects: [instance]);
        var result = Resolve(Resolver([RawDamage()]), new CombatRequest(Guid.NewGuid(), instance, current, 2),
            new ScriptedDice(6));
        var health = Assert.Single(result.Changes.OfType<HealthChanged>());
        Assert.Equal(20, health.PreviousHealth);
        Assert.Equal(11, health.CurrentHealth);
        Assert.Equal(50, instance.SourceSnapshot!.Health);
        Assert.Empty(result.Changes.OfType<CombatantDefeated>());
    }

    [Fact]
    public void Tick_RemovedEffectIsRejectedBeforeConsumingDice()
    {
        var effect = new CombatEffectSnapshot(new EffectInstanceId(Guid.NewGuid()), Periodic(), Combatant(50));
        var dice = new ScriptedDice();
        var outcome = Resolver([RawDamage()]).Resolve(new CombatRequest(Guid.NewGuid(), effect, Combatant(50), 1), dice);
        Assert.Equal(CombatRejectionReason.InvalidState, Assert.IsType<RejectedCombat>(outcome).Rejection.Reason);
        Assert.Empty(dice.Trace.Rolls);
    }

    [Fact]
    public void Tick_DefeatedCapturedSourceDoesNotApplyAttackInitiationRules()
    {
        var effect = new CombatEffectSnapshot(new EffectInstanceId(Guid.NewGuid()), Periodic(), Combatant(0));
        var target = Combatant(3, effects: [effect]);
        var result = Resolve(Resolver([RawDamage()]), new CombatRequest(Guid.NewGuid(), effect, target, 1), new ScriptedDice(6));
        Assert.Equal(0, Assert.Single(result.Changes.OfType<HealthChanged>()).CurrentHealth);
        Assert.Equal(target.Id, Assert.Single(result.Changes.OfType<CombatantDefeated>()).CombatantId);
    }

    [Fact]
    public void Tick_CannotProposeHealthOrStatusChangesToHistoricalSource()
    {
        var source = Combatant(50);
        var effect = new CombatEffectSnapshot(new EffectInstanceId(Guid.NewGuid()), Periodic(), source);
        var request = new CombatRequest(Guid.NewGuid(), effect, Combatant(50, effects: [effect]), 1);
        var health = new DelegateStep(context => context with
        {
            HealthAdjustments = context.HealthAdjustments.Add(new HealthAdjustment(source.Id, -1))
        });
        var status = new DelegateStep(context => context with
        {
            StateChanges = context.StateChanges.Add(new EffectRemoved(source.Id, effect.Id))
        });
        Assert.Throws<InvalidOperationException>(() => Resolver([health]).Resolve(request, new ScriptedDice()));
        Assert.Throws<InvalidOperationException>(() => Resolver([status]).Resolve(request, new ScriptedDice()));
    }

    [Fact]
    public void Tick_AttackSpecificStepsFailClearlyWithoutConsumingDice()
    {
        var effect = new CombatEffectSnapshot(new EffectInstanceId(Guid.NewGuid()), Periodic(), Combatant(50));
        var request = new CombatRequest(Guid.NewGuid(), effect, Combatant(50, effects: [effect]), 1);
        foreach (var step in new ICombatStep[] { new AttackRollStep(new AttackRollResolver()),
                     new AttackOutcomeStep(new D20AttackOutcomePolicy()), StatusApplication() })
        {
            var dice = new ScriptedDice();
            Assert.Throws<InvalidOperationException>(() => Resolver([step]).Resolve(request, dice));
            Assert.Empty(dice.Trace.Rolls);
        }
    }

    [Fact]
    public void TickContracts_RejectMissingCaptureNonPeriodicEffectsAndInvalidArguments()
    {
        var source = Combatant(50);
        var id = new EffectInstanceId(Guid.NewGuid());
        var uncaptured = new CombatEffectSnapshot(id, Periodic());
        var ordinary = new CombatEffectSnapshot(id, new TestDefinition(), source);
        var captured = new CombatEffectSnapshot(id, Periodic(), source);
        Assert.Throws<ArgumentException>(() => new CombatRequest(Guid.NewGuid(), uncaptured, source, 1));
        Assert.Throws<ArgumentException>(() => new CombatRequest(Guid.NewGuid(), ordinary, source, 1));
        Assert.Throws<ArgumentException>(() => new CombatRequest(Guid.Empty, captured, source, 1));
        Assert.Throws<ArgumentOutOfRangeException>(() => new CombatRequest(Guid.NewGuid(), captured, source, -1));
        Assert.Throws<ArgumentNullException>(() => new CombatRequest(Guid.NewGuid(), null!, source, 1));
        Assert.Throws<ArgumentNullException>(() => new CombatRequest(Guid.NewGuid(), captured, null!, 1));
        Assert.Throws<ArgumentException>(() => new PeriodicDamageEffectDefinition(new EffectDefinitionId(Guid.NewGuid()), []));
        Assert.Throws<ArgumentException>(() => new PeriodicDamageEffectDefinition(new EffectDefinitionId(Guid.NewGuid()), [null!]));
    }

    [Fact]
    public void PeriodicDefinition_CopiesFormulasInsteadOfRetainingMutableCollection()
    {
        var components = new[] { new DamageComponent(new DiceFormula(1, 6), 3, DamageType.Fire) };
        var definition = new PeriodicDamageEffectDefinition(new EffectDefinitionId(Guid.NewGuid()), components);
        components[0] = new DamageComponent(new DiceFormula(10, 20), 100, DamageType.Cold);
        Assert.Equal(3, Assert.Single(definition.Damage).FlatDamage);
    }

    private static PeriodicDamageEffectDefinition Periodic(EffectRecipient recipient = EffectRecipient.Target) =>
        new(new EffectDefinitionId(Guid.NewGuid()), [new DamageComponent(new DiceFormula(1, 6), 3, DamageType.Fire)], recipient);

    private static CombatantSnapshot WithState(CombatantSnapshot previous, int health, int? armor = null,
        IReadOnlyList<ResistanceValue>? resistances = null, IReadOnlyList<CombatEffectSnapshot>? effects = null) =>
        new(previous.Id, health, previous.MaxHealth, armor ?? previous.Armor, previous.Evasion,
            resistances ?? previous.Resistances, effects ?? previous.Effects);

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
}
