using RollForHonor.Domain.Combat.Attacks.Models;
using RollForHonor.Domain.Combat.Attacks.Services;
using RollForHonor.Domain.Combat.Damage.Configuration;
using RollForHonor.Domain.Combat.Damage.Models;
using RollForHonor.Domain.Combat.Damage.Services;
using RollForHonor.Domain.Combat.Resolution.Models;
using RollForHonor.Domain.Combat.Resolution.Services;
using RollForHonor.Domain.Combat.Resolution.Services.Steps;
using RollForHonor.Domain.Combat.State.Changes;
using RollForHonor.Domain.Dice.Models;

namespace RollForHonor.Domain.Tests.Combat.Resolution;

public sealed partial class CombatPipelineTests
{
    [Fact]
    public void RawDamage_ResolvesThroughFinalizationWithoutAttackRollOrOutcome_AtUnscaledDamage()
    {
        var request = CreateRequest(damage:
        [
            new DamageComponent(new DiceFormula(2, 6), 3, DamageType.Slashing),
            new DamageComponent(new DiceFormula(1, 4), -10, DamageType.Fire)
        ]);
        var dice = new CountingDice();

        var result = Resolve(Resolver([RawDamage()]), request, dice);

        Assert.Null(result.Result.Target.AttackRoll);
        Assert.Null(result.Result.Target.Outcome);
        var damage = result.Result.Target.DamageResolution!;
        Assert.Equal(new[] { 15, 0 }, damage.BeforeDefenses.Damage.Components.Select(c => c.Amount));
        Assert.Equal(new[] { DamageType.Slashing, DamageType.Fire }, damage.BeforeDefenses.Damage.Components.Select(c => c.Type));
        Assert.Equal(15, damage.AfterDefenses.Sum());
        Assert.Equal(DamageStepType.WeaponRoll, Assert.Single(damage.BeforeDefenses.Breakdown.Steps).Type);
        Assert.Equal(new[] { 6, 4 }, dice.Trace.Rolls.Select(r => r.Formula.Sides));
        Assert.Equal(35, Assert.Single(result.Changes.OfType<HealthChanged>()).CurrentHealth);
        Assert.Equal(50, request.StateSnapshot.Target.Health);
    }

    [Fact]
    public void RawDamage_OmittingQualityStepDoesNotImplicitlyScaleAnExistingOutcome()
    {
        var result = Resolve(Resolver([
            new AttackRollStep(new AttackRollResolver()),
            new AttackOutcomeStep(new D20AttackOutcomePolicy()),
            RawDamage(),
            new DefenseStep(new DefenseResolver(new HitMagnitudeArmorFormula()))
        ]), CreateRequest(target: Combatant(50, armor: 50)), new CountingDice());

        Assert.Equal(AttackOutcome.CriticalHit, result.Result.Target.Outcome);
        Assert.Equal(10, result.Result.Target.DamageResolution!.BeforeDefenses.Sum());
        Assert.Equal(5, result.Result.Target.DamageResolution.AfterDefenses.Sum());
        Assert.DoesNotContain(result.Result.Target.DamageResolution.BeforeDefenses.Breakdown.Steps,
            step => step.Type == DamageStepType.AttackQuality);
    }

    [Theory]
    [InlineData(AttackOutcome.Evaded, 0)]
    [InlineData(AttackOutcome.GlancingHit, 5)]
    [InlineData(AttackOutcome.Hit, 10)]
    [InlineData(AttackOutcome.CriticalHit, 20)]
    public void QualityStep_UsesOnlyOutcomeAndRawDamage_WithoutAnAttackRoll(AttackOutcome outcome, int expected)
    {
        var initial = new CombatContext(CreateRequest()) { AttackOutcome = outcome };
        var dice = new CountingDice();
        var raw = RawDamage().Execute(initial, dice);

        var scaled = QualityDamage().Execute(raw, dice);

        Assert.Null(scaled.AttackRoll);
        Assert.Equal(expected, scaled.BeforeDefenses!.Sum());
        Assert.Equal(10, raw.BeforeDefenses!.Sum());
        Assert.Single(raw.BeforeDefenses.Breakdown.Steps);
        Assert.Equal(DamageStepType.AttackQuality, scaled.BeforeDefenses.Breakdown.Steps[1].Type);
        Assert.Single(dice.Trace.Rolls);
        Assert.Null(initial.BeforeDefenses);
    }

    [Fact]
    public void QualityStep_RoundsEachTypedComponentUpward_AndPreservesHistory()
    {
        var request = CreateRequest(damage:
        [
            new DamageComponent(new DiceFormula(1, 4), -1, DamageType.Piercing),
            new DamageComponent(new DiceFormula(1, 4), -3, DamageType.Cold)
        ]);
        var dice = new CountingDice();
        var raw = RawDamage().Execute(new CombatContext(request) { AttackOutcome = AttackOutcome.GlancingHit }, dice);

        var result = QualityDamage().Execute(raw, dice);

        Assert.Equal(new[] { 2, 1 }, result.BeforeDefenses!.Damage.Components.Select(c => c.Amount));
        Assert.Equal(new[] { DamageType.Piercing, DamageType.Cold }, result.BeforeDefenses.Damage.Components.Select(c => c.Type));
        Assert.Same(raw.BeforeDefenses!.Breakdown.Steps[0], result.BeforeDefenses.Breakdown.Steps[0]);
        Assert.Equal(new[] { 3, 1 }, raw.BeforeDefenses.Damage.Components.Select(c => c.Amount));
        Assert.Equal(2, dice.Trace.Rolls.Count);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public void QualityStep_RejectsMissingDamageOrOutcome_RepeatScaling_AndExecutionAfterDefenses(int invalid)
    {
        var dice = new CountingDice();
        var initial = new CombatContext(CreateRequest()) { AttackOutcome = AttackOutcome.Hit };
        var raw = RawDamage().Execute(initial, dice);
        var quality = QualityDamage();
        var context = invalid switch
        {
            0 => initial,
            1 => raw with { AttackOutcome = null },
            2 => quality.Execute(raw, dice),
            _ => new DefenseStep(new DefenseResolver(new HitMagnitudeArmorFormula())).Execute(raw, dice)
        };
        var rollsBefore = dice.Trace.Rolls.Count;

        Assert.Throws<InvalidOperationException>(() => quality.Execute(context, dice));
        Assert.Equal(rollsBefore, dice.Trace.Rolls.Count);
        Assert.Equal(10, raw.BeforeDefenses!.Sum());
    }

    [Fact]
    public void RawDamage_RejectsRecalculationWithoutAdditionalDice()
    {
        var dice = new CountingDice();
        var raw = RawDamage().Execute(new CombatContext(CreateRequest()), dice);

        Assert.Throws<InvalidOperationException>(() => RawDamage().Execute(raw, dice));
        Assert.Single(dice.Trace.Rolls);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    [InlineData(6)]
    public void IndependentDamagePipeline_AcceptsEffectsAtEveryBoundary(int position)
    {
        var request = CreateRequest(effects: [new TestDefinition()]);
        var calls = 0;
        var effects = new ApplyEffectsStep([new ProbeEffect((context, _) =>
        {
            calls++;
            Assert.Equal(position >= 1, context.AttackRoll is not null);
            Assert.Equal(position >= 2, context.AttackOutcome is not null);
            Assert.Equal(position >= 3, context.BeforeDefenses is not null);
            Assert.Equal(position >= 4,
                context.BeforeDefenses?.Breakdown.Steps.Any(s => s.Type == DamageStepType.AttackQuality) ?? false);
            Assert.Equal(position >= 5, context.AfterDefenses is not null);
            Assert.Equal(position >= 6, context.NewEffectsResolved);
            return context;
        })]);
        var steps = IndependentAttackSteps().ToList();
        steps.Add(new DefenseStep(new DefenseResolver(new HitMagnitudeArmorFormula())));
        steps.Add(StatusApplication());
        steps.Insert(position, effects);
        var dice = new CountingDice();

        var result = Resolve(Resolver(steps), request, dice);

        Assert.Equal(1, calls);
        Assert.Equal(20, result.Result.Target.DamageResolution!.AfterDefenses.Sum());
        Assert.Equal(30, Assert.Single(result.Changes.OfType<HealthChanged>()).CurrentHealth);
        Assert.Equal(2, dice.Trace.Rolls.Count);
        var qualityTrace = result.StepTrace.Single(t => t.Name == nameof(AttackQualityStep));
        Assert.Equal(qualityTrace.DiceStartIndex, qualityTrace.DiceEndIndex);
    }

    [Theory]
    [InlineData(AttackOutcome.Evaded, 0, 0, 50)]
    [InlineData(AttackOutcome.GlancingHit, 5, 2, 48)]
    [InlineData(AttackOutcome.Hit, 10, 5, 45)]
    [InlineData(AttackOutcome.CriticalHit, 20, 14, 36)]
    public void IndependentDamagePipeline_PreservesAttackDamageThroughDefensesAndHealth(
        AttackOutcome outcome, int expectedBefore, int expectedAfter, int expectedHealth)
    {
        var request = CreateRequest(target: Combatant(50, armor: 50));
        var setOutcome = new ApplyEffectsStep([new ProbeEffect((context, _) => context with { AttackOutcome = outcome })]);
        var attackRoll = new AttackRollStep(new AttackRollResolver());
        var defense = new DefenseStep(new DefenseResolver(new HitMagnitudeArmorFormula()));
        var dice = new CountingDice();
        var result = Resolve(Resolver([attackRoll, setOutcome, RawDamage(), QualityDamage(), defense]), request, dice);

        var damage = result.Result.Target.DamageResolution!;
        Assert.Equal(expectedBefore, damage.BeforeDefenses.Sum());
        Assert.Equal(expectedAfter, damage.AfterDefenses.Sum());
        if (expectedAfter == 0)
            Assert.Empty(result.Changes);
        else
            Assert.Equal(expectedHealth, Assert.Single(result.Changes.OfType<HealthChanged>()).CurrentHealth);
        Assert.Equal(new[] { DamageStepType.WeaponRoll, DamageStepType.AttackQuality,
            DamageStepType.ArmorMitigation, DamageStepType.ResistanceMitigation },
            damage.AfterDefenses.Breakdown.Steps.Select(s => s.Type));
        Assert.Equal(new[] { 20, 10 }, dice.Trace.Rolls.Select(r => r.Formula.Sides));
    }

    [Fact]
    public void QualityStep_ScalingOverflowDoesNotAlterRawDamageOrConsumeMoreDice()
    {
        var request = CreateRequest(damage: [new DamageComponent(new DiceFormula(1, 2), int.MaxValue - 2, DamageType.Fire)]);
        var dice = new CountingDice();
        var raw = RawDamage().Execute(new CombatContext(request) { AttackOutcome = AttackOutcome.CriticalHit }, dice);

        Assert.Throws<OverflowException>(() => QualityDamage().Execute(raw, dice));
        Assert.Equal(int.MaxValue, raw.BeforeDefenses!.Sum());
        Assert.Single(raw.BeforeDefenses.Breakdown.Steps);
        Assert.Single(dice.Trace.Rolls);
    }

    private static RawDamageStep RawDamage() => new(new RawDamageResolver());
    private static AttackQualityStep QualityDamage() => new(new AttackQualityDamageResolver(OutcomeMultipliers()));
    private static DamageMultipliers OutcomeMultipliers() => new(0, 0.5m, 1, 2);
    private static ICombatStep[] IndependentAttackSteps() =>
    [
        new AttackRollStep(new AttackRollResolver()),
        new AttackOutcomeStep(new D20AttackOutcomePolicy()),
        RawDamage(), QualityDamage()
    ];
}
