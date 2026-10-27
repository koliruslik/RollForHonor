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

namespace RollForHonor.Domain.Tests.Combat.Resolution;

public sealed class CombatPipelineTests(ITestOutputHelper output) : CombatDiagnosticTestBase(output)
{
    [Fact]
    public void Resolve_WithProductionSteps_CombinesDamageLifestealAndReflectionOnce()
    {
        var request = CreateRequest(source: Combatant(50, armor: int.MaxValue), target: Combatant(50, armor: 50, resistances:
            [new ResistanceValue(DamageType.Slashing, 0.5m)]));
        var resolver = Resolver(CalculationSteps().Concat([
            new DefenseStep(new DefenseResolver(new HitMagnitudeArmorFormula())),
            new ApplyEffectsStep([new LifestealEffect(0.5m), new ReflectDamageEffect()], "AfterDefensesEffects")
        ]));
        var dice = new CountingDice();

        var resolved = Resolve(resolver, request, dice);

        var damage = Assert.IsType<DamageResolution>(resolved.Result.Target.DamageResolution);
        Assert.Equal(10, damage.BeforeDefenses.Sum());
        Assert.Equal(3, damage.AfterDefenses.Sum());
        var health = resolved.Changes.OfType<HealthChanged>().ToArray();
        Assert.Equal(2, health.Length);
        Assert.Equal(request.SourceId, health[0].CombatantId);
        Assert.Equal(49, health[0].CurrentHealth);
        Assert.Equal(request.TargetId, health[1].CombatantId);
        Assert.Equal(47, health[1].CurrentHealth);
        Assert.Equal(2, dice.Trace.Rolls.Count);
        Assert.Equal(50, request.StateSnapshot.Source.Health);
        Assert.Equal(50, request.StateSnapshot.Target.Health);
        var effects = Assert.Single(resolved.StepTrace.Where(t => t.Name == "AfterDefensesEffects"));
        Assert.Equal(new[] { nameof(LifestealEffect), nameof(ReflectDamageEffect) },
            effects.Children.Select(t => t.Name));
        Assert.All(effects.Children, t => Assert.Equal(t.DiceStartIndex, t.DiceEndIndex));
    }

    [Fact]
    public void Resolve_WhenDefenseStepIsOmitted_UsesPreDefenseDamage()
    {
        var request = CreateRequest(target: Combatant(50, armor: int.MaxValue));

        var result = Resolve(Resolver(CalculationSteps()), request, new CountingDice());

        Assert.Equal(40, Assert.Single(result.Changes.OfType<HealthChanged>()).CurrentHealth);
        Assert.DoesNotContain(result.StepTrace, t => t.Name == nameof(DefenseStep));
    }

    [Fact]
    public void Resolve_HealingAssemblyHasRollButNoDamageOutcomeOrDefense()
    {
        var definition = new TestDefinition();
        var request = CreateRequest(effects: [definition], damage: []);
        var resolver = Resolver([
            new AttackRollStep(new AttackRollResolver()),
            new ApplyEffectsStep([new HealFromRollEffect()], "HealingEffects")
        ]);
        var dice = new CountingDice();

        var result = Resolve(resolver, request, dice);

        Assert.Equal(60, Assert.Single(result.Changes.OfType<HealthChanged>()).CurrentHealth);
        Assert.NotNull(result.Result.Target.AttackRoll);
        Assert.Null(result.Result.Target.Outcome);
        Assert.Null(result.Result.Target.DamageResolution);
        Assert.Single(dice.Trace.Rolls);
        Assert.DoesNotContain(result.StepTrace, t => t.Name is nameof(AttackDamageStep) or nameof(DefenseStep));
        var text = CombatResolutionDiagnosticFormatter.Format(new ResolvedCombat(result));
        Assert.Contains("Damage     : none", text);
        Assert.Contains("HealingEffects", text);
    }

    [Fact]
    public void Resolve_NewDefinitionReplacesExistingStatusAndSelectsSourceOrTarget()
    {
        var targetDefinition = new TestDefinition();
        var old = new CombatEffectSnapshot(new EffectInstanceId(Guid.NewGuid()), targetDefinition);
        var sourceDefinition = new TestDefinition(EffectRecipient.Source);
        var target = Combatant(50, effects: [old]);
        var request = CreateRequest(target: target, effects: [targetDefinition, sourceDefinition], damage: []);
        var resolver = Resolver([
            new ApplyNewEffectsStep(new CombatEffectResolver(new ResolutionEffectInstanceIdFactory()))
        ]);

        var first = Resolve(resolver, request, new CountingDice());
        var repeated = Resolve(resolver, request, new CountingDice());

        var removal = Assert.Single(first.Changes.OfType<EffectRemoved>());
        Assert.Equal(old.Id, removal.EffectId);
        Assert.Equal(target.Id, removal.CombatantId);
        var applied = first.Changes.OfType<EffectApplied>().ToArray();
        Assert.Equal(2, applied.Length);
        Assert.Equal(target.Id, applied[0].CombatantId);
        Assert.Equal(request.SourceId, applied[1].CombatantId);
        Assert.NotEqual(old.Id, applied[0].Effect.Id);
        Assert.Equal(applied.Select(a => a.Effect.Id),
            repeated.Changes.OfType<EffectApplied>().Select(a => a.Effect.Id));
        Assert.Same(old, Assert.Single(target.Effects));
        Assert.Empty(first.Changes.OfType<HealthChanged>());
        Assert.Equal(2, first.Result.Target.Effects.Count);
    }

    [Fact]
    public void ApplyNewEffects_WhenDefinitionOccursTwice_ReplacesTheEarlierProposal()
    {
        var definition = new TestDefinition();
        var request = CreateRequest(effects: [definition, definition], damage: []);
        var initial = new CombatContext(request);
        var step = new ApplyNewEffectsStep(new CombatEffectResolver(new ResolutionEffectInstanceIdFactory()));

        var result = step.Execute(initial, new CountingDice());

        var additions = result.StateChanges.OfType<EffectApplied>().ToArray();
        var removal = Assert.Single(result.StateChanges.OfType<EffectRemoved>());
        Assert.Equal(2, additions.Length);
        Assert.Equal(additions[0].Effect.Id, removal.EffectId);
        Assert.NotEqual(additions[0].Effect.Id, additions[1].Effect.Id);
        Assert.Empty(initial.StateChanges);
        Assert.False(initial.NewEffectsResolved);
        Assert.True(result.NewEffectsResolved);
        Assert.Throws<InvalidOperationException>(() => step.Execute(result, new CountingDice()));
    }

    [Fact]
    public void ApplyEffects_CanBeInsertedBeforeRollAndBeforeDefenses_WithoutMutatingEarlierContexts()
    {
        var initial = new CombatContext(CreateRequest());
        var dice = new CountingDice();
        var steps = new CombatPipeline([
            new ApplyEffectsStep([], "BeforeRollEffects"),
            new CombatPipeline(CalculationSteps(), "RawDamage"),
            new ApplyEffectsStep([new DoubleDamageEffect()], "BeforeDefensesEffects")
        ]);

        var result = steps.Execute(initial, dice);

        Assert.Null(initial.AttackRoll);
        Assert.Null(initial.BeforeDefenses);
        Assert.Empty(initial.StepTrace);
        Assert.Equal(20, result.BeforeDefenses!.Sum());
        Assert.Equal(new[] { "BeforeRollEffects", "RawDamage", "BeforeDefensesEffects" },
            result.StepTrace.Select(t => t.Name));
        var raw = result.StepTrace[1];
        Assert.Equal(0, raw.DiceStartIndex);
        Assert.Equal(2, raw.DiceEndIndex);
        Assert.Equal(new[] { nameof(AttackRollStep), nameof(AttackOutcomeStep), nameof(AttackDamageStep) },
            raw.Children.Select(t => t.Name));
        Assert.Equal(1, raw.Children[0].DiceEndIndex);
        Assert.Equal(1, raw.Children[1].DiceStartIndex);
        Assert.Equal(1, raw.Children[1].DiceEndIndex);
    }

    [Fact]
    public void Pipeline_CopiesCompositionAndPassesEachReturnedContextToTheNextStep()
    {
        var initial = new CombatContext(CreateRequest());
        var steps = CalculationSteps().ToList();
        var pipeline = new CombatPipeline(steps);
        steps.Clear();

        var result = pipeline.Execute(initial, new CountingDice());

        Assert.NotNull(result.AttackRoll);
        Assert.NotNull(result.AttackOutcome);
        Assert.NotNull(result.BeforeDefenses);
        Assert.Same(initial.Request, result.Request);
        Assert.Null(initial.AttackRoll);
        Assert.Same(initial, new CombatPipeline([]).Execute(initial, new CountingDice()));
    }

    [Fact]
    public void MisorderedOrRepeatedSteps_FailBeforeConsumingAdditionalDice()
    {
        var initial = new CombatContext(CreateRequest());
        var dice = new CountingDice();
        Assert.Throws<InvalidOperationException>(() => new DefenseStep(
            new DefenseResolver(new HitMagnitudeArmorFormula())).Execute(initial, dice));
        Assert.Throws<InvalidOperationException>(() => new AttackOutcomeStep(new D20AttackOutcomePolicy()).Execute(initial, dice));
        Assert.Throws<InvalidOperationException>(() => new AttackDamageStep(
            new AttackDamageResolver(new DamageMultipliers(0, 1, 1, 1))).Execute(initial, dice));
        Assert.Empty(dice.Trace.Rolls);

        var rolled = new AttackRollStep(new AttackRollResolver()).Execute(initial, dice);
        Assert.Throws<InvalidOperationException>(() => new AttackRollStep(new AttackRollResolver()).Execute(rolled, dice));
        Assert.Single(dice.Trace.Rolls);
        var classified = new AttackOutcomeStep(new D20AttackOutcomePolicy()).Execute(rolled, dice);
        Assert.Throws<InvalidOperationException>(() => new AttackOutcomeStep(new D20AttackOutcomePolicy()).Execute(classified, dice));
        Assert.Single(dice.Trace.Rolls);
    }

    [Fact]
    public void ResultBuilder_IsRepeatableWithoutCollectingDamageTwiceOrMutatingContext()
    {
        var initial = new CombatContext(CreateRequest());
        var dice = new CountingDice();
        var calculated = new CombatPipeline(CalculationSteps()).Execute(initial, dice);
        var builder = new CombatResolutionBuilder(new HealthStateProjector());

        var first = builder.Build(calculated, dice.Trace);
        var repeated = builder.Build(calculated, dice.Trace);

        Assert.Equal(40, Assert.Single(first.Changes.OfType<HealthChanged>()).CurrentHealth);
        Assert.Equal(first.Changes.ToArray(), repeated.Changes.ToArray());
        Assert.Empty(calculated.HealthAdjustments);
        Assert.Empty(calculated.StateChanges);
        Assert.Equal(3, calculated.StepTrace.Length);
        Assert.Equal(4, first.StepTrace.Length);
        var finalization = first.StepTrace[^1];
        Assert.Equal(nameof(CombatResolutionBuilder), finalization.Name);
        Assert.Equal(new[] { "CollectHealthAdjustments", "ProjectHealth", "BuildStateChanges", "BuildCombatResult" },
            finalization.Children.Select(t => t.Name));
        Assert.Equal(2, dice.Trace.Rolls.Count);
    }

    [Fact]
    public void ResultBuilder_RejectsUnknownRecipientsAndPrebuiltHealthChanges()
    {
        var request = CreateRequest();
        var context = new CombatContext(request)
        {
            HealthAdjustments = [new HealthAdjustment(new CombatantId(Guid.NewGuid()), 1)]
        };
        var builder = new CombatResolutionBuilder(new HealthStateProjector());
        var trace = new DiceTrace([]);
        Assert.Throws<InvalidOperationException>(() => builder.Build(context, trace));
        Assert.Throws<InvalidOperationException>(() => builder.Build(new CombatContext(request) with
        {
            StateChanges = [new HealthChanged(request.TargetId, 50, 49)]
        }, trace));
        Assert.Throws<InvalidOperationException>(() => builder.Build(new CombatContext(request) with
        {
            StateChanges = [new EffectRemoved(new CombatantId(Guid.NewGuid()), new EffectInstanceId(Guid.NewGuid()))]
        }, trace));
    }

    [Fact]
    public void Resolve_SelfTargetIsProjectedOnceWithCombinedDamageAndHealing()
    {
        var participant = Combatant(50);
        var request = CreateRequest(source: participant, target: participant);
        var resolver = Resolver(CalculationSteps().Concat([
            new ApplyEffectsStep([new HealFromRollEffect()])
        ]));

        var result = Resolve(resolver, request, new CountingDice());

        Assert.Empty(result.Changes);
        Assert.False(result.Result.Target.IsDefeated);
        Assert.Equal(50, participant.Health);
    }

    [Fact]
    public void Pipeline_RejectsNullResultsAndRequestReplacement_AndStopsFollowingSteps()
    {
        var initial = new CombatContext(CreateRequest());
        var dice = new CountingDice();
        var calls = 0;
        var following = new DelegateStep(c => { calls++; return c; });
        Assert.Throws<InvalidOperationException>(() => new CombatPipeline([
            new DelegateStep(_ => null!), following
        ]).Execute(initial, dice));
        Assert.Throws<InvalidOperationException>(() => new CombatPipeline([
            new DelegateStep(_ => new CombatContext(CreateRequest())), following
        ]).Execute(initial, dice));
        Assert.Equal(0, calls);
        Assert.Empty(initial.StepTrace);
    }

    [Fact]
    public void Resolver_AlwaysFinalizesEvenWhenCalculationPipelineIsEmpty()
    {
        var result = Resolve(Resolver([]), CreateRequest(), new CountingDice());

        Assert.Empty(result.Changes);
        Assert.Null(result.Result.Target.DamageResolution);
        Assert.Equal(nameof(CombatResolutionBuilder), Assert.Single(result.StepTrace).Name);
    }

    [Fact]
    public void Pipeline_RejectsOverwrittenTraceBeforeRunningFurtherSteps()
    {
        var calls = 0;
        var pipeline = new CombatPipeline([
            new AttackRollStep(new AttackRollResolver()),
            new DelegateStep(context => context with { StepTrace = [] }),
            new DelegateStep(context => { calls++; return context; })
        ]);

        Assert.Throws<InvalidOperationException>(() =>
            pipeline.Execute(new CombatContext(CreateRequest()), new CountingDice()));

        Assert.Equal(0, calls);
    }

    private CombatResolution Resolve(CombatResolver resolver, CombatRequest request, IDiceRoller dice) =>
        Assert.IsType<ResolvedCombat>(LogOutcome(resolver.Resolve(request, dice))).Resolution;

    private static CombatResolver Resolver(IEnumerable<ICombatStep> calculation) =>
        new(new CombatRequestValidator(), new CombatPipeline(calculation),
            new CombatResolutionBuilder(new HealthStateProjector()));

    private static ICombatStep[] CalculationSteps() =>
    [
        new AttackRollStep(new AttackRollResolver()),
        new AttackOutcomeStep(new D20AttackOutcomePolicy()),
        new AttackDamageStep(new AttackDamageResolver(new DamageMultipliers(0m, 1m, 1m, 1m)))
    ];

    private static CombatRequest CreateRequest(CombatantSnapshot? source = null, CombatantSnapshot? target = null,
        IReadOnlyList<CombatEffectDefinition>? effects = null, IReadOnlyList<DamageComponent>? damage = null)
    {
        source ??= Combatant(50);
        target ??= Combatant(50);
        return new CombatRequest(Guid.NewGuid(), source.Id, target.Id,
            new AttackPayload(new DiceFormula(1, 20), 0, NaturalOutcomeShiftRules.Standard,
                damage ?? [new DamageComponent(new DiceFormula(1, 10), 0, DamageType.Slashing)],
                effects ?? [], AttackTags.Melee), new CombatStateSnapshot(1, source, target));
    }

    private static CombatantSnapshot Combatant(int health, int armor = 0,
        IReadOnlyList<ResistanceValue>? resistances = null, IReadOnlyList<CombatEffectSnapshot>? effects = null) =>
        new(new CombatantId(Guid.NewGuid()), health, 100, armor, 0, resistances ?? [], effects ?? []);

    private sealed record TestDefinition : CombatEffectDefinition
    {
        public TestDefinition(EffectRecipient recipient = EffectRecipient.Target)
            : base(new EffectDefinitionId(Guid.NewGuid()), recipient) { }
    }

    private sealed class CountingDice : IDiceRoller
    {
        private readonly List<DiceRoll> _rolls = [];
        public DiceTrace Trace => new(_rolls);
        public DiceRoll Roll(DiceFormula formula)
        {
            var results = Enumerable.Repeat(Math.Min(10, formula.Sides), formula.Count).ToArray();
            var roll = new DiceRoll(formula, results, results.Sum());
            _rolls.Add(roll);
            return roll;
        }
    }

    private sealed class LifestealEffect(decimal fraction) : ICombatEffect
    {
        public CombatContext Apply(CombatContext context, IDiceRoller dice)
        {
            var amount = checked((int)Math.Ceiling(context.AfterDefenses!.Sum() * fraction));
            return context with
            {
                HealthAdjustments = context.HealthAdjustments.Add(new HealthAdjustment(context.Request.SourceId, amount))
            };
        }
    }

    private sealed class ReflectDamageEffect : ICombatEffect
    {
        public CombatContext Apply(CombatContext context, IDiceRoller dice) => context with
        {
            HealthAdjustments = context.HealthAdjustments.Add(
                new HealthAdjustment(context.Request.SourceId, -context.AfterDefenses!.Sum()))
        };
    }

    private sealed class HealFromRollEffect : ICombatEffect
    {
        public CombatContext Apply(CombatContext context, IDiceRoller dice) => context with
        {
            HealthAdjustments = context.HealthAdjustments.Add(
                new HealthAdjustment(context.Request.TargetId, context.AttackRoll!.Total))
        };
    }

    private sealed class DoubleDamageEffect : ICombatEffect
    {
        public CombatContext Apply(CombatContext context, IDiceRoller dice)
        {
            var before = context.BeforeDefenses!;
            var packet = new DamagePacket(before.Damage.Components.Select(c => new DamageAmount(c.Type, c.Amount * 2)).ToArray());
            var history = new DamageBreakdown(before.Breakdown.Steps.Append(
                new DamageStep(DamageStepType.AttackerModifier, before.Damage, packet)).ToArray());
            return context with { BeforeDefenses = new DamageCalculationResult(history, packet) };
        }
    }

    private sealed class DelegateStep(Func<CombatContext, CombatContext> execute) : ICombatStep
    {
        public CombatContext Execute(CombatContext context, IDiceRoller dice) => execute(context);
    }
}
