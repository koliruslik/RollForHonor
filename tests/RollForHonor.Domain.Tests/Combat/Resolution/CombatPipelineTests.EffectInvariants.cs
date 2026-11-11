using System.Collections.Immutable;
using RollForHonor.Domain.Combat.Attacks.Models;
using RollForHonor.Domain.Combat.Attacks.Services;
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

namespace RollForHonor.Domain.Tests.Combat.Resolution;

public sealed partial class CombatPipelineTests
{
    [Theory]
    [InlineData(0)] // Before rolling.
    [InlineData(1)] // Between roll and outcome.
    [InlineData(2)] // Between outcome and raw damage.
    [InlineData(3)] // Between raw damage and attack quality.
    [InlineData(4)] // Between attack quality and defenses.
    [InlineData(5)] // Between defenses and status application.
    [InlineData(6)] // Between status application and mandatory finalization.
    public void Effects_RunAtEveryCalculationBoundary_WithOnlyResultsAvailableAtThatPoint(int position)
    {
        var request = CreateRequest(effects: [new TestDefinition()]);
        var calls = 0;
        var effect = new ProbeEffect((context, _) =>
        {
            calls++;
            Assert.Equal(position >= 1, context.AttackRoll is not null);
            Assert.Equal(position >= 2, context.AttackOutcome is not null);
            Assert.Equal(position >= 3, context.BeforeDefenses is not null);
            Assert.Equal(position >= 4, context.BeforeDefenses?.Breakdown.Steps.Any(s => s.Type == DamageStepType.AttackQuality) ?? false);
            Assert.Equal(position >= 5, context.AfterDefenses is not null);
            Assert.Equal(position >= 6, context.NewEffectsResolved);
            Assert.DoesNotContain(context.StateChanges, c => c is HealthChanged or CombatantDefeated);
            return context with
            {
                HealthAdjustments = context.HealthAdjustments.Add(new HealthAdjustment(request.SourceId, 7))
            };
        });
        var steps = CalculationSteps().Concat<ICombatStep>([
            new DefenseStep(new DefenseResolver(new HitMagnitudeArmorFormula())),
            StatusApplication()
        ]).ToList();
        steps.Insert(position, new ApplyEffectsStep([effect]));

        var result = Resolve(Resolver(steps), request, new CountingDice());

        Assert.Equal(1, calls);
        Assert.Equal(57, result.Changes.OfType<HealthChanged>().Single(c => c.CombatantId == request.SourceId).CurrentHealth);
        Assert.Single(result.Changes.OfType<EffectApplied>());
        Assert.Equal(50, request.StateSnapshot.Source.Health);
    }

    [Fact]
    public void Effects_ModifiedRollIsConsumedByOutcome_WithoutRerolling()
    {
        var modifyRoll = new ProbeEffect((context, _) =>
        {
            var roll = context.AttackRoll!;
            return context with { AttackRoll = new AttackRollResult(roll.Roll, -11, roll.Roll.Total - 11) };
        });
        var dice = new CountingDice();
        var result = Resolve(Resolver([
            new AttackRollStep(new AttackRollResolver()),
            new ApplyEffectsStep([modifyRoll]),
            new AttackOutcomeStep(new D20AttackOutcomePolicy())
        ]), CreateRequest(), dice);

        Assert.Equal(-1, result.Result.Target.AttackRoll!.Total);
        Assert.Equal(AttackOutcome.GlancingHit, result.Result.Target.Outcome);
        Assert.Single(dice.Trace.Rolls);
    }

    [Fact]
    public void Effects_ModifiedOutcomeIsConsumedByDamage_WithoutRepeatingEarlierSteps()
    {
        var steps = CalculationSteps();
        var dice = new CountingDice();
        var result = Resolve(Resolver([
            steps[0], steps[1],
            new ApplyEffectsStep([new ProbeEffect((context, _) => context with { AttackOutcome = AttackOutcome.Evaded })]),
            steps[2], steps[3]
        ]), CreateRequest(), dice);

        Assert.Equal(AttackOutcome.Evaded, result.Result.Target.Outcome);
        Assert.Equal(0, result.Result.Target.DamageResolution!.AfterDefenses.Sum());
        Assert.Empty(result.Changes);
        Assert.Equal(2, dice.Trace.Rolls.Count);
    }

    [Theory]
    [InlineData(true, 14, 36)] // Scale raw 10 -> 20, then armor 50 -> 14.
    [InlineData(false, 10, 40)] // Armor 50 reduces 10 -> 5, then scale -> 10.
    public void Effects_DamageModificationUsesItsScheduledSideOfDefenses(bool beforeDefense, int damage, int health)
    {
        var request = CreateRequest(target: Combatant(50, armor: 50));
        var effect = new ProbeEffect((context, _) => beforeDefense
            ? context with { BeforeDefenses = ScaleDamage(context.BeforeDefenses!, 2) }
            : context with { AfterDefenses = ScaleDamage(context.AfterDefenses!, 2) });
        var defense = new DefenseStep(new DefenseResolver(new HitMagnitudeArmorFormula()));
        var effects = new ApplyEffectsStep([effect]);
        ICombatStep[] tail = beforeDefense ? [effects, defense] : [defense, effects];

        var result = Resolve(Resolver(CalculationSteps().Concat(tail)), request, new CountingDice());

        Assert.Equal(damage, result.Result.Target.DamageResolution!.AfterDefenses.Sum());
        Assert.Equal(health, Assert.Single(result.Changes.OfType<HealthChanged>()).CurrentHealth);
        Assert.Contains(result.Result.Target.DamageResolution.AfterDefenses.Breakdown.Steps,
            s => s.Type == DamageStepType.AttackerModifier);
        Assert.Equal(50, request.StateSnapshot.Target.Armor);
    }

    [Theory]
    [InlineData(false, 30)] // (10 + 5) * 2
    [InlineData(true, 25)]  // 10 * 2 + 5
    public void Effects_InOneGroupRunInOrder_AndConsumeThePreviousHandlersContext(bool reverse, int expected)
    {
        var add = new ProbeEffect((context, _) => context with
        {
            HealthAdjustments = context.HealthAdjustments.Add(new HealthAdjustment(context.Request.SourceId, 5))
        });
        var multiply = new ProbeEffect((context, _) => context with
        {
            HealthAdjustments = context.HealthAdjustments.Select(a => new HealthAdjustment(a.CombatantId, a.Amount * 2))
                .ToImmutableArray()
        });
        var initial = new CombatContext(CreateRequest());
        initial = initial with { HealthAdjustments = [new HealthAdjustment(initial.Request.SourceId, 10)] };
        ICombatEffect[] handlers = reverse ? [multiply, add] : [add, multiply];
        var group = new ApplyEffectsStep(handlers);
        handlers[0] = new ProbeEffect((_, _) => throw new InvalidOperationException("Composition was not copied."));

        var result = group.Execute(initial, new CountingDice());

        Assert.Equal(expected, result.HealthAdjustments.Sum(a => a.Amount));
        Assert.Equal(10, Assert.Single(initial.HealthAdjustments).Amount);
    }

    [Fact]
    public void Effects_IdentityHandlerPreservesCalculationsAndProposals_AndAnEmptyGroupIsValid()
    {
        var dice = new CountingDice();
        var calculated = new CombatPipeline(CalculationSteps()).Execute(new CombatContext(CreateRequest()), dice);
        var group = new ApplyEffectsStep([new ProbeEffect((context, _) => context)]);

        var result = group.Execute(calculated, dice);

        Assert.Same(calculated.BeforeDefenses, result.BeforeDefenses);
        Assert.Equal(calculated.HealthAdjustments, result.HealthAdjustments);
        Assert.Equal(calculated.StateChanges, result.StateChanges);
        Assert.Same(result, new ApplyEffectsStep([]).Execute(result, dice));
        Assert.Equal(2, dice.Trace.Rolls.Count);
    }

    [Fact]
    public void Effects_UseSharedDiceAndKeepNestedTraceRanges_AndAreDeterministic()
    {
        var request = CreateRequest();
        var rollEffect = new ProbeEffect((context, dice) => context with
        {
            HealthAdjustments = context.HealthAdjustments.Add(
                new HealthAdjustment(context.Request.SourceId, dice.Roll(new DiceFormula(1, 6)).Total))
        });
        var resolver = Resolver(CalculationSteps().Concat([
            new ApplyEffectsStep([rollEffect, rollEffect], "RolledEffects")
        ]));

        var first = Resolve(resolver, request, new CountingDice());
        var second = Resolve(resolver, request, new CountingDice());

        Assert.Equal(first.Changes.ToArray(), second.Changes.ToArray());
        Assert.Equal(62, first.Changes.OfType<HealthChanged>().Single(c => c.CombatantId == request.SourceId).CurrentHealth);
        Assert.Equal(new[] { 20, 10, 6, 6 }, first.DiceTrace.Rolls.Select(r => r.Formula.Sides));
        var group = first.StepTrace.Single(t => t.Name == "RolledEffects");
        Assert.Equal(2, group.DiceStartIndex);
        Assert.Equal(4, group.DiceEndIndex);
        Assert.Collection(group.Children,
            child => { Assert.Equal(2, child.DiceStartIndex); Assert.Equal(3, child.DiceEndIndex); },
            child => { Assert.Equal(3, child.DiceStartIndex); Assert.Equal(4, child.DiceEndIndex); });
    }

    [Theory]
    [InlineData(-100, 70, 20)] // No premature defeat/clamp at zero.
    [InlineData(100, -70, 80)] // No premature clamp at maximum.
    public void Effects_HealthProposalsAreCombinedBeforeOneClamp_RegardlessOfOrder(int first, int second, int expected)
    {
        var request = CreateRequest();
        ProbeEffect Adjustment(int amount) => new((context, _) => context with
        {
            HealthAdjustments = context.HealthAdjustments.Add(new HealthAdjustment(request.TargetId, amount))
        });
        var forward = Resolve(Resolver([new ApplyEffectsStep([Adjustment(first), Adjustment(second)])]), request, new CountingDice());
        var reverse = Resolve(Resolver([new ApplyEffectsStep([Adjustment(second), Adjustment(first)])]), request, new CountingDice());

        Assert.Equal(expected, Assert.Single(forward.Changes.OfType<HealthChanged>()).CurrentHealth);
        Assert.Equal(forward.Changes.ToArray(), reverse.Changes.ToArray());
        Assert.Empty(forward.Changes.OfType<CombatantDefeated>());
        Assert.Equal(50, request.StateSnapshot.Target.Health);
    }

    [Fact]
    public void Effects_StatusReplacementUsesEarlierProposals_AndIsIsolatedByRecipientAndDefinition()
    {
        var definition = new TestDefinition();
        var unrelated = new TestDefinition();
        var sourceInstance = new CombatEffectSnapshot(new EffectInstanceId(Guid.NewGuid()), definition);
        var removedEarlier = new CombatEffectSnapshot(new EffectInstanceId(Guid.NewGuid()), definition);
        var proposedEarlier = new CombatEffectSnapshot(new EffectInstanceId(Guid.NewGuid()), definition);
        var unrelatedInstance = new CombatEffectSnapshot(new EffectInstanceId(Guid.NewGuid()), unrelated);
        var request = CreateRequest(source: Combatant(50, effects: [sourceInstance]),
            target: Combatant(50, effects: [removedEarlier, unrelatedInstance]), effects: [definition]);
        var initial = new CombatContext(request)
        {
            StateChanges = [new EffectRemoved(request.TargetId, removedEarlier.Id),
                new EffectApplied(request.TargetId, proposedEarlier)]
        };

        var result = StatusApplication().Execute(initial, new CountingDice());

        Assert.Equal(new[] { removedEarlier.Id, proposedEarlier.Id },
            result.StateChanges.OfType<EffectRemoved>().Select(c => c.EffectId));
        Assert.DoesNotContain(result.StateChanges.OfType<EffectRemoved>(),
            c => c.EffectId == sourceInstance.Id || c.EffectId == unrelatedInstance.Id);
        Assert.All(result.StateChanges.OfType<EffectRemoved>(), c => Assert.Equal(request.TargetId, c.CombatantId));
        Assert.Equal(2, initial.StateChanges.Length);
        Assert.Equal(2, request.StateSnapshot.Target.Effects.Count);
        Assert.Same(sourceInstance, Assert.Single(request.StateSnapshot.Source.Effects));
    }

    [Fact]
    public void Effects_SelfTargetStatusIsAppliedOnce_EvenThoughSourceAndTargetAreTheSameParticipant()
    {
        var definition = new TestDefinition();
        var old = new CombatEffectSnapshot(new EffectInstanceId(Guid.NewGuid()), definition);
        var participant = Combatant(50, effects: [old]);

        var result = Resolve(Resolver([StatusApplication()]),
            CreateRequest(source: participant, target: participant, effects: [definition]), new CountingDice());

        Assert.Equal(old.Id, Assert.Single(result.Changes.OfType<EffectRemoved>()).EffectId);
        Assert.Equal(participant.Id, Assert.Single(result.Changes.OfType<EffectApplied>()).CombatantId);
        Assert.Empty(result.Changes.OfType<HealthChanged>());
        Assert.Same(old, Assert.Single(participant.Effects));
    }

    [Theory]
    [InlineData(0)] // Throw after an earlier proposal.
    [InlineData(1)] // Return null.
    [InlineData(2)] // Replace the request.
    [InlineData(3)] // Erase an earlier trace record.
    public void Effects_InvalidHandlerStopsRemainingHandlersAndFinalization(int failure)
    {
        var request = CreateRequest();
        var nextCalls = 0;
        var invalid = new ProbeEffect((context, _) => failure switch
        {
            0 => throw new InvalidOperationException("Test handler failed."),
            1 => null!,
            2 => new CombatContext(CreateRequest()),
            _ => context with { StepTrace = [] }
        });
        var pipeline = new CombatPipeline([
            new AttackRollStep(new AttackRollResolver()),
            new ApplyEffectsStep([
                new ProbeEffect((context, _) => context with
                {
                    HealthAdjustments = context.HealthAdjustments.Add(new HealthAdjustment(request.TargetId, -100))
                }),
                invalid,
                new ProbeEffect((context, _) => { nextCalls++; return context; })
            ])
        ]);

        Assert.Throws<InvalidOperationException>(() => Resolver([pipeline]).Resolve(request, new CountingDice()));

        Assert.Equal(0, nextCalls);
        Assert.Equal(50, request.StateSnapshot.Target.Health);
    }

    [Fact]
    public void Effects_DamageTransformationPreservesMixedTypesAndHistory_ForFollowingDefenses()
    {
        var request = CreateRequest(target: Combatant(50, armor: 50,
            resistances: [new ResistanceValue(DamageType.Fire, 0.5m)]), damage:
        [
            new DamageComponent(new DiceFormula(1, 10), 0, DamageType.Slashing),
            new DamageComponent(new DiceFormula(1, 4), 0, DamageType.Fire)
        ]);
        var result = Resolve(Resolver(CalculationSteps().Concat([
            new ApplyEffectsStep([new ProbeEffect((context, _) => context with
            {
                BeforeDefenses = ScaleDamage(context.BeforeDefenses!, 2)
            })]),
            new DefenseStep(new DefenseResolver(new HitMagnitudeArmorFormula()))
        ])), request, new CountingDice());

        var damage = result.Result.Target.DamageResolution!;
        Assert.Equal(new[] { DamageType.Slashing, DamageType.Fire }, damage.AfterDefenses.Damage.Components.Select(c => c.Type));
        Assert.Equal(new[] { 20, 8 }, damage.BeforeDefenses.Damage.Components.Select(c => c.Amount));
        Assert.Equal(new[] { 14, 4 }, damage.AfterDefenses.Damage.Components.Select(c => c.Amount));
        Assert.Equal(32, Assert.Single(result.Changes.OfType<HealthChanged>()).CurrentHealth);
        Assert.Equal(damage.BeforeDefenses.Breakdown.Steps.ToArray(),
            damage.AfterDefenses.Breakdown.Steps.Take(damage.BeforeDefenses.Breakdown.Steps.Count).ToArray());
    }

    [Theory]
    [InlineData(0)] // Foreign health recipient.
    [InlineData(1)] // Foreign status recipient.
    [InlineData(2)] // Handler prebuilds a health change.
    [InlineData(3)] // Handler prebuilds defeat.
    [InlineData(4)] // Post-defense damage without original damage.
    public void Effects_FinalizationRejectsInvalidProposals_InsteadOfReturningPartialSuccess(int invalidProposal)
    {
        var request = CreateRequest();
        var foreign = new CombatantId(Guid.NewGuid());
        var effect = new ProbeEffect((context, _) => invalidProposal switch
        {
            0 => context with { HealthAdjustments = [new HealthAdjustment(foreign, -10)] },
            1 => context with { StateChanges = [new EffectRemoved(foreign, new EffectInstanceId(Guid.NewGuid()))] },
            2 => context with { StateChanges = [new HealthChanged(request.TargetId, 50, 40)] },
            3 => context with { StateChanges = [new CombatantDefeated(request.TargetId)] },
            _ => context with { AfterDefenses = new CombatPipeline(CalculationSteps())
                .Execute(new CombatContext(request), new CountingDice()).BeforeDefenses }
        });

        Assert.Throws<InvalidOperationException>(() =>
            Resolver([new ApplyEffectsStep([effect])]).Resolve(request, new CountingDice()));
        Assert.Equal(50, request.StateSnapshot.Target.Health);
    }

    [Fact]
    public void Effects_HitConditionsBelongToComposition_NotImplicitStatusApplicationPolicy()
    {
        var steps = CalculationSteps();
        var request = CreateRequest(effects: [new TestDefinition()]);
        var result = Resolve(Resolver([
            steps[0], steps[1],
            new ApplyEffectsStep([new ProbeEffect((context, _) => context with { AttackOutcome = AttackOutcome.Evaded })]),
            steps[2], steps[3], StatusApplication()
        ]), request, new CountingDice());

        Assert.Equal(AttackOutcome.Evaded, result.Result.Target.Outcome);
        Assert.Equal(0, result.Result.Target.DamageResolution!.AfterDefenses.Sum());
        Assert.Single(result.Changes.OfType<EffectApplied>());
        Assert.Empty(result.Changes.OfType<HealthChanged>());
    }

    [Fact]
    public void Effects_RejectedRequestNeverInvokesHandlersOrConsumesDice()
    {
        var calls = 0;
        var source = Combatant(0);
        var request = CreateRequest(source: source);
        var dice = new CountingDice();
        var resolver = Resolver([new ApplyEffectsStep([new ProbeEffect((context, _) => { calls++; return context; })])]);

        Assert.IsType<RejectedCombat>(resolver.Resolve(request, dice));
        Assert.Equal(0, calls);
        Assert.Empty(dice.Trace.Rolls);
    }

    [Fact]
    public void Effects_StatusApplicationDoesNotExecuteOrRediscoverExistingOrNewBehaviors()
    {
        var definition = new TestDefinition();
        var old = new CombatEffectSnapshot(new EffectInstanceId(Guid.NewGuid()), definition);
        var request = CreateRequest(target: Combatant(50, effects: [old]), effects: [definition]);
        var calls = 0;
        var scheduled = new ProbeEffect((context, _) => { calls++; return context; });

        var result = Resolve(Resolver([
            new ApplyEffectsStep([scheduled]), StatusApplication(), new ApplyEffectsStep([])
        ]), request, new CountingDice());

        Assert.Equal(1, calls);
        Assert.Single(result.Changes.OfType<EffectApplied>());
        Assert.Single(result.Changes.OfType<EffectRemoved>());
        Assert.Empty(result.Changes.OfType<HealthChanged>());
        Assert.Same(old, Assert.Single(request.StateSnapshot.Target.Effects));
    }

    [Theory]
    [InlineData(0, 5, AttackOutcome.CriticalHit)] // Armor 0 -> 50, raw 10 -> 5.
    [InlineData(1, 10, AttackOutcome.Hit)] // Evasion 0 -> 5 changes the outcome.
    [InlineData(2, 5, AttackOutcome.CriticalHit)] // Resistance 0 -> 0.5.
    public void Effects_TestConsumerBuildsFreshStatsForNextAttack_WithoutAccumulatingBonuses(
        int stat, int expectedDamage, AttackOutcome expectedOutcome)
    {
        var baseTarget = Combatant(50);
        var definition = new TestStatDefinition(stat);
        var application = Resolve(Resolver([StatusApplication()]),
            CreateRequest(target: baseTarget, effects: [definition], damage: []), new CountingDice());
        var active = Assert.Single(application.Changes.OfType<EffectApplied>()).Effect;

        // Test-only consumer boundary: apply the proposed status, then project from BASE stats.
        // It is not a production commit service, scheduler, or general stat aggregation policy.
        var projected = ProjectTestStats(baseTarget, [active]);
        var projectedAgain = ProjectTestStats(baseTarget, [active]);
        var resolver = Resolver(CalculationSteps().Concat([
            new DefenseStep(new DefenseResolver(new HitMagnitudeArmorFormula()))
        ]));
        var baseline = Resolve(resolver, CreateRequest(target: baseTarget), new CountingDice());
        var nextInput = CreateRequest(target: projected);
        var nextRequest = new CombatRequest(nextInput.ResolutionId, nextInput.SourceId, nextInput.TargetId,
            Assert.IsType<AttackPayload>(nextInput.Attack), new CombatStateSnapshot(application.BasedOnStateVersion + 1,
                nextInput.StateSnapshot.Source, projected));
        var next = Resolve(resolver, nextRequest, new CountingDice());
        var rebuilt = Resolve(resolver, CreateRequest(target: projectedAgain), new CountingDice());
        var expired = Resolve(resolver, CreateRequest(target: ProjectTestStats(baseTarget, [])), new CountingDice());

        Assert.Equal(10, baseline.Result.Target.DamageResolution!.AfterDefenses.Sum());
        Assert.Equal(expectedDamage, next.Result.Target.DamageResolution!.AfterDefenses.Sum());
        Assert.Equal(expectedOutcome, next.Result.Target.Outcome);
        Assert.Equal(next.Result.Target.DamageResolution.AfterDefenses.Sum(), rebuilt.Result.Target.DamageResolution!.AfterDefenses.Sum());
        Assert.Equal(next.Result.Target.Outcome, rebuilt.Result.Target.Outcome);
        Assert.Equal(baseline.Result.Target.DamageResolution.AfterDefenses.Sum(), expired.Result.Target.DamageResolution!.AfterDefenses.Sum());
        Assert.Equal(baseline.Result.Target.Outcome, expired.Result.Target.Outcome);
        Assert.Empty(baseTarget.Effects);
        Assert.Equal(0, baseTarget.Armor);
        Assert.Equal(0, baseTarget.Evasion);
        Assert.Empty(baseTarget.Resistances);
        Assert.Equal(50, baseTarget.Health);
        Assert.Equal(baseTarget.Id, projected.Id);
        Assert.Equal(2, next.BasedOnStateVersion);
        Assert.Equal(1, application.BasedOnStateVersion);
        Assert.Equal(projected.Armor, projectedAgain.Armor);
        Assert.Equal(projected.Evasion, projectedAgain.Evasion);
        Assert.Equal(projected.Resistances.ToArray(), projectedAgain.Resistances.ToArray());
        Assert.Same(active, Assert.Single(projected.Effects));
        Assert.Empty(application.Changes.OfType<HealthChanged>());
    }

    private static ApplyNewEffectsStep StatusApplication() =>
        new(new CombatEffectResolver(new ResolutionEffectInstanceIdFactory()));

    [Fact]
    public void StatusApplication_RejectsHealthChangingResolverOutput_WithoutChangingContext()
    {
        var initial = new CombatContext(CreateRequest());
        var step = new ApplyNewEffectsStep(new HealthChangingStatusResolver());
        var dice = new CountingDice();

        Assert.Throws<InvalidOperationException>(() => step.Execute(initial, dice));
        Assert.False(initial.NewEffectsResolved);
        Assert.Empty(initial.HealthAdjustments);
        Assert.Empty(initial.StateChanges);
        Assert.Empty(dice.Trace.Rolls);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void CalculationSteps_RejectRepeatedDamageAndDefense_WithoutMoreDice(bool repeatDefense)
    {
        var dice = new CountingDice();
        var raw = new CombatPipeline(CalculationSteps()).Execute(new CombatContext(CreateRequest()), dice);
        var defense = new DefenseStep(new DefenseResolver(new HitMagnitudeArmorFormula()));
        var context = repeatDefense ? defense.Execute(raw, dice) : raw;
        ICombatStep repeated = repeatDefense ? defense : CalculationSteps()[2];
        var rollsBefore = dice.Trace.Rolls.Count;

        Assert.Throws<InvalidOperationException>(() => repeated.Execute(context, dice));
        Assert.Equal(rollsBefore, dice.Trace.Rolls.Count);
        Assert.Same(raw.BeforeDefenses, context.BeforeDefenses);
    }

    [Fact]
    public void QualityStep_RejectsMissingOutcome_AfterRollAndRawDamage()
    {
        var dice = new CountingDice();
        var rolled = new AttackRollStep(new AttackRollResolver()).Execute(new CombatContext(CreateRequest()), dice);
        var raw = RawDamage().Execute(rolled, dice);

        Assert.Throws<InvalidOperationException>(() => CalculationSteps()[3].Execute(raw, dice));
        Assert.Equal(2, dice.Trace.Rolls.Count);
        Assert.Single(raw.BeforeDefenses!.Breakdown.Steps);
    }

    [Fact]
    public void Finalization_RejectsUnknownChangeTypes_FromAnEffectHandler()
    {
        var effect = new ProbeEffect((context, _) => context with { StateChanges = [new UnsupportedChange()] });

        Assert.Throws<InvalidOperationException>(() =>
            Resolver([new ApplyEffectsStep([effect])]).Resolve(CreateRequest(), new CountingDice()));
    }

    private sealed record UnsupportedChange : ICombatStateChange;

    private sealed class HealthChangingStatusResolver : ICombatEffectResolver
    {
        public EffectResolution Resolve(CombatContext context) =>
            new([], [new HealthAdjustment(context.Request.TargetId, -10)], []);
    }

    private static DamageCalculationResult ScaleDamage(DamageCalculationResult original, int factor)
    {
        var packet = new DamagePacket(original.Damage.Components
            .Select(c => new DamageAmount(c.Type, checked(c.Amount * factor))).ToArray());
        var history = new DamageBreakdown(original.Breakdown.Steps.Append(
            new DamageStep(DamageStepType.AttackerModifier, original.Damage, packet)).ToArray());
        return new DamageCalculationResult(history, packet);
    }

    private static CombatantSnapshot ProjectTestStats(CombatantSnapshot baseline, IReadOnlyList<CombatEffectSnapshot> active)
    {
        var stats = active.Select(e => e.Definition).OfType<TestStatDefinition>().ToArray();
        return new CombatantSnapshot(baseline.Id, baseline.Health, baseline.MaxHealth,
            baseline.Armor + stats.Count(e => e.Stat == 0) * 50,
            baseline.Evasion + stats.Count(e => e.Stat == 1) * 5,
            stats.Any(e => e.Stat == 2) ? [new ResistanceValue(DamageType.Slashing, 0.5m)] : baseline.Resistances,
            active);
    }

    private sealed record TestStatDefinition(int Stat)
        : CombatEffectDefinition(new EffectDefinitionId(Guid.NewGuid()), EffectRecipient.Target);

    private sealed class ProbeEffect(Func<CombatContext, IDiceRoller, CombatContext> apply) : ICombatEffect
    {
        public CombatContext Apply(CombatContext context, IDiceRoller dice) => apply(context, dice);
    }
}
