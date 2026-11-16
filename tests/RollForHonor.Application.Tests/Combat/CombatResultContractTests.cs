using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
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
using RollForHonor.Domain.Combat.State.Services;
using RollForHonor.Domain.Dice.Models;
using RollForHonor.Domain.Dice.Services;
using RollForHonor.Domain.Resources.Models;
using RollForHonor.Domain.Resources.Services;
using RollForHonor.Domain.Tests.Diagnostics;
using RollForHonor.Infrastructure.Diagnostics;
using Xunit.Abstractions;

namespace RollForHonor.Application.Tests.Combat;

/// <summary>Verifies existing return values supply presentation facts without journal models, log parsing or rerolls.</summary>
public sealed class CombatResultContractTests : CombatDiagnosticTestBase
{
    /// <summary>Creates contract tests using the established opt-in outcome logger.</summary>
    /// <param name="output">The current xUnit output.</param>
    public CombatResultContractTests(ITestOutputHelper output) : base(output) { }

    /// <summary>Natural d20, typed damage and exact component dice remain available independently of optional diagnostics.</summary>
    /// <param name="diagnosticsEnabled">Whether full diagnostic capture and JSONL export are enabled.</param>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ExistingResultsExposeFactsWithoutInferringGlobalDiceOrder(bool diagnosticsEnabled)
    {
        using var writer = new StringWriter();
        var rig = new Rig(diagnostics: diagnosticsEnabled ? new LoggerCombatDiagnostics(NullLogger.Instance, jsonl: writer) : null);
        var attack = rig.Start();
        var result = Impact(attack, rig.Target.Id);
        var resolution = ((ResolvedCombat)result.Outcome).Resolution;
        var target = resolution.Result.Target;
        var roll = target.AttackRoll!;
        var damage = target.DamageResolution!;
        var raw = Assert.Single(damage.BeforeDefenses.Breakdown.Steps, step => step.Type == DamageStepType.WeaponRoll);
        var bonus = Assert.Single(damage.BeforeDefenses.Breakdown.Steps, step => step.Type == DamageStepType.AttackerModifier);

        Assert.True(result.IsCommitted);
        Assert.Equal(4, resolution.DiceTrace.Rolls[0].Total);
        Assert.Equal(13, Assert.Single(roll.Roll.Results));
        Assert.Equal(4, roll.Modifier);
        Assert.Equal(17, roll.Total);
        Assert.Equal(AttackOutcome.Hit, target.Outcome);
        Assert.Equal(new[] { DamageType.Slashing, DamageType.Fire }, raw.ComponentRolls.Select(value => value.Component.Type));
        Assert.Equal(new[] { 2, 5 }, raw.ComponentRolls[0].Roll.Results);
        Assert.Equal(3, raw.ComponentRolls[0].Component.FlatDamage);
        Assert.Equal(10, raw.ComponentRolls[0].Amount);
        Assert.Equal(3, Assert.Single(raw.ComponentRolls[1].Roll.Results));
        Assert.Equal(1, raw.ComponentRolls[1].Component.FlatDamage);
        Assert.Same(resolution.DiceTrace.Rolls[2], raw.ComponentRolls[0].Roll);
        Assert.Same(resolution.DiceTrace.Rolls[3], raw.ComponentRolls[1].Roll);
        Assert.Equal("fixture:rune", bonus.Origin);
        Assert.Equal(2, bonus.FlatBonus);
        Assert.Equal(6, Assert.Single(Assert.Single(bonus.ComponentRolls).Roll.Results));
        Assert.Equal(8, Assert.Single(bonus.ComponentRolls).Amount);
        Assert.Equal(new[] { new DamageAmount(DamageType.Slashing, 18), new DamageAmount(DamageType.Fire, 2) },
            damage.AfterDefenses.Damage.Components);
        var names = new Dictionary<CombatantId, string> { [rig.Source.Id] = "Hero", [rig.Target.Id] = "Goblin" };
        Assert.Equal("Hero", names[resolution.Result.SourceId]);
        Assert.Equal("Goblin", names[target.TargetId]);
        Assert.Single(target.Effects);
        Assert.Contains(resolution.Changes.OfType<EffectApplied>(), change => change.CombatantId == rig.Target.Id);
        Assert.Equal(1, rig.DiceCreated);
        Assert.Equal(5, resolution.DiceTrace.Rolls.Count);
        Assert.Equal(30, rig.State.Get(rig.Target.Id).Health);
        if (diagnosticsEnabled)
        {
            var lines = writer.ToString().Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries);
            Assert.Equal(2, lines.Length);
            using var report = JsonDocument.Parse(lines[1]);
            Assert.Contains("componentRolls", report.RootElement.GetRawText());
        }
        else Assert.Empty(writer.ToString());
    }

    /// <summary>Hit quality and typed remaining damage are explicit facts for Godot styling.</summary>
    /// <param name="naturalRoll">The actual attack die value.</param>
    /// <param name="outcome">The expected resolved quality.</param>
    /// <param name="damage">The expected total after quality and typed defenses.</param>
    [Theory]
    [InlineData(1, AttackOutcome.Evaded, 0)]
    [InlineData(6, AttackOutcome.GlancingHit, 10)]
    [InlineData(13, AttackOutcome.Hit, 20)]
    [InlineData(20, AttackOutcome.CriticalHit, 40)]
    public void NaturalRollQualityAndDamageRemainSeparateFacts(int naturalRoll, AttackOutcome outcome, int damage)
    {
        var rig = new Rig(attackRoll: naturalRoll);
        var result = Impact(rig.Start(), rig.Target.Id);
        var target = ((ResolvedCombat)result.Outcome).Resolution.Result.Target;
        Assert.True(result.IsCommitted);
        Assert.Equal(naturalRoll, Assert.Single(target.AttackRoll!.Roll.Results));
        Assert.Equal(outcome, target.Outcome);
        Assert.Equal(damage, target.DamageResolution!.AfterDefenses.Sum());
    }

    /// <summary>Later live-state changes cannot alter the retained facts of an earlier committed impact.</summary>
    [Fact]
    public void HistoricalResultDoesNotReadCurrentParticipantState()
    {
        var rig = new Rig();
        var result = Impact(rig.Start(), rig.Target.Id);
        var resolution = ((ResolvedCombat)result.Outcome).Resolution;
        rig.State.ExternalUpdate(new(rig.Target.Id, 7, 50, 1000, 30, [new(DamageType.Fire, 1m)], []));

        Assert.Equal(7, rig.State.Get(rig.Target.Id).Health);
        Assert.Equal(20, resolution.Result.Target.DamageResolution!.AfterDefenses.Sum());
        Assert.Equal(30, Assert.Single(resolution.Changes.OfType<HealthChanged>()).CurrentHealth);
        var raw = resolution.Result.Target.DamageResolution.BeforeDefenses.Breakdown.Steps
            .Single(step => step.Type == DamageStepType.WeaponRoll);
        Assert.Equal(new[] { 2, 5 }, raw.ComponentRolls[0].Roll.Results);
        Assert.Equal(3, raw.ComponentRolls[0].Component.FlatDamage);
        Assert.Equal(1, rig.DiceCreated);
    }

    /// <summary>Overkill damage is distinguishable from the actual clamped health transition.</summary>
    [Fact]
    public void DamageAmountAndAppliedHealthLossAreNotConflated()
    {
        var rig = new Rig(targetHealth: 3);
        var result = Impact(rig.Start(), rig.Target.Id);
        var resolution = ((ResolvedCombat)result.Outcome).Resolution;
        var health = Assert.Single(resolution.Changes.OfType<HealthChanged>());
        Assert.Equal(20, resolution.Result.Target.DamageResolution!.AfterDefenses.Sum());
        Assert.Equal(3, health.PreviousHealth);
        Assert.Equal(0, health.CurrentHealth);
        Assert.True(resolution.Result.Target.IsDefeated);
        Assert.Contains(resolution.Changes.OfType<CombatantDefeated>(), change => change.CombatantId == rig.Target.Id);
    }

    /// <summary>A completed calculation with a version conflict cannot be mistaken for newly applied player-facing damage.</summary>
    [Fact]
    public void ConflictRetainsCalculationWithoutClaimingAppliedDamage()
    {
        var rig = new Rig();
        rig.State.BeforeCommit = _ => rig.State.ExternalUpdate(rig.Target);
        var result = Impact(rig.Start(), rig.Target.Id);
        Assert.IsType<ResolvedCombat>(result.Outcome);
        Assert.Equal(CombatCommitStatus.VersionConflict, result.CommitStatus);
        Assert.False(result.IsCommitted);
        Assert.Equal(50, rig.State.Get(rig.Target.Id).Health);
    }

    /// <summary>A replay receipt distinguishes already-published changes from a new state transition.</summary>
    [Fact]
    public void AlreadyAppliedReceiptDoesNotDescribeANewPublication()
    {
        var rig = new Rig();
        rig.State.BeforeCommit = resolution =>
        {
            rig.State.BeforeCommit = null;
            Assert.Equal(CombatCommitStatus.Applied, rig.State.TryCommit(resolution));
        };
        var result = Impact(rig.Start(), rig.Target.Id);
        Assert.Equal(CombatCommitStatus.AlreadyApplied, result.CommitStatus);
        Assert.False(result.IsCommitted);
        Assert.Single(rig.State.Committed);
        Assert.Equal(30, rig.State.Get(rig.Target.Id).Health);
    }

    /// <summary>Domain rejection has no fabricated attack roll, damage calculation or commit attempt.</summary>
    [Fact]
    public void RejectedImpactHasNoAppliedResult()
    {
        var rig = new Rig();
        var attack = rig.Start();
        rig.State.ExternalUpdate(new(rig.Source.Id, 0, 100, 0, 15, [], []));
        var result = Impact(attack, rig.Target.Id);
        Assert.IsType<RejectedCombat>(result.Outcome);
        Assert.Null(result.CommitStatus);
        Assert.False(result.IsCommitted);
        Assert.Equal(0, rig.State.CommitAttempts);
    }

    /// <summary>Ignored contacts have no impact to turn into a damage or d20 entry.</summary>
    /// <param name="endFirst">Whether the instance ends before contact rather than consuming a target once.</param>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void IgnoredContactsHaveNoSyntheticResolution(bool endFirst)
    {
        var rig = new Rig();
        var attack = rig.Start(new(AttackRepeatPolicy.OncePerTarget));
        if (endFirst) attack.End();
        else Impact(attack, rig.Target.Id);
        var ignored = attack.ResolveContact(rig.Target.Id);
        Assert.Null(ignored.Impact);
        Assert.Equal(endFirst ? AttackContactStatus.AttackEnded : AttackContactStatus.TargetAlreadyContacted, ignored.Status);
        Assert.Equal(endFirst ? 0 : 1, rig.DiceCreated);
    }

    /// <summary>Unreported modifier attribution remains absent instead of being invented by presentation.</summary>
    [Fact]
    public void UnknownOriginsRemainUnknown()
    {
        var rig = new Rig();
        var result = Impact(rig.Start(), rig.Target.Id);
        var damage = ((ResolvedCombat)result.Outcome).Resolution.Result.Target.DamageResolution!;
        var raw = Assert.Single(damage.BeforeDefenses.Breakdown.Steps, step => step.Type == DamageStepType.WeaponRoll);
        Assert.Null(raw.Origin);
        Assert.Equal(3, raw.ComponentRolls[0].Component.FlatDamage);
        Assert.Equal("fixture:rune", damage.BeforeDefenses.Breakdown.Steps.Single(step => step.Type == DamageStepType.AttackerModifier).Origin);
    }

    /// <summary>A periodic resolution retains damage dice without inventing an attack d20 or hit-quality result.</summary>
    [Fact]
    public void PeriodicResultCanOmitD20AndStillExposeComponentFacts()
    {
        var source = Character(0);
        var definition = Burning();
        var effect = new CombatEffectSnapshot(new(Guid.NewGuid()), definition, source);
        var target = new CombatantSnapshot(new(Guid.NewGuid()), 10, 10, 0, 15, [new(DamageType.Fire, 0.5m)], [effect]);
        var request = new CombatRequest(Guid.NewGuid(), effect, target, 0);
        var pipeline = new CombatPipeline([new RawDamageStep(new RawDamageResolver()), new DefenseStep(new DefenseResolver(new HitMagnitudeArmorFormula()))]);
        var resolver = new CombatResolver(new CombatRequestValidator(), pipeline, new CombatResolutionBuilder(new HealthStateProjector()));
        var result = LogOutcome(resolver.Resolve(request, new ScriptedDice(3)));
        var resolution = Assert.IsType<ResolvedCombat>(result).Resolution;
        Assert.Null(resolution.Result.Target.AttackRoll);
        Assert.Null(resolution.Result.Target.Outcome);
        var raw = resolution.Result.Target.DamageResolution!.BeforeDefenses.Breakdown.Steps.Single();
        Assert.Equal(3, Assert.Single(Assert.Single(raw.ComponentRolls).Roll.Results));
        Assert.Equal(1, Assert.Single(raw.ComponentRolls).Component.FlatDamage);
        Assert.Equal(2, resolution.Result.Target.DamageResolution.AfterDefenses.Sum());
        Assert.Equal(target.Id, Assert.Single(resolution.Changes.OfType<HealthChanged>()).CombatantId);
    }

    private CombatImpactResult Impact(AttackInstance attack, CombatantId target)
    {
        var contact = attack.ResolveContact(target);
        Assert.Equal(AttackContactStatus.Processed, contact.Status);
        var result = Assert.IsType<CombatImpactResult>(contact.Impact);
        LogOutcome(result.Outcome);
        return result;
    }

    private static CombatantSnapshot Character(int health = 100) => new(new(Guid.NewGuid()), health, 100, 0, 15, [], []);
    private static PeriodicDamageEffectDefinition Burning() => new(new(Guid.NewGuid()), [new(new(1, 6), 1, DamageType.Fire)]);

    private sealed class Rig
    {
        internal CombatantSnapshot Source { get; } = Character();
        internal CombatantSnapshot Target { get; }
        internal TestCombatStateAdapter State { get; }
        internal AttackInitiationService Starts { get; }
        internal int DiceCreated { get; private set; }

        internal Rig(int targetHealth = 50, int attackRoll = 13, ICombatDiagnostics? diagnostics = null)
        {
            Target = new(new(Guid.NewGuid()), targetHealth, 50, 0, 15, [new(DamageType.Fire, 0.5m)], []);
            State = new(Source, Target);
            var impacts = new CombatImpactService(State, new CombatSnapshotBuilder(), new ContractPipeline(), () =>
            {
                DiceCreated++;
                return new ScriptedDice(4, attackRoll, 2, 5, 3, 6);
            }, diagnostics);
            Starts = new(State, new ResourceCostResolver(new AdditiveResourceCostPolicy()), new NoReductions(), impacts,
                () => TimeSpan.Zero, diagnostics);
        }

        internal AttackInstance Start(AttackExecutionRules? rules = null)
        {
            var payload = new AttackPayload(new(1, 20), 4, NaturalOutcomeShiftRules.Standard,
                [new(new(2, 6), 3, DamageType.Slashing), new(new(1, 4), 1, DamageType.Fire)], [Burning()], AttackTags.Projectile);
            var result = Starts.Start(Guid.NewGuid(), Source.Id, new("contract", payload, TimeSpan.Zero, [], rules));
            Assert.Equal(AttackStartStatus.Started, result.Status);
            return result.Attack!;
        }
    }

    private sealed class ContractPipeline : ICombatPipelineFactory
    {
        /// <inheritdoc />
        public ICombatStep Create(AttackInstance attack, CombatStateSnapshot snapshot) => new CombatPipeline([
            new ApplyEffectsStep([new UnrelatedDiceEffect()], "Unrelated roll"),
            new AttackRollStep(new AttackRollResolver()), new AttackOutcomeStep(new D20AttackOutcomePolicy()),
            new RawDamageStep(new RawDamageResolver()), new ApplyEffectsStep([new RuneDamageEffect()]),
            new AttackQualityStep(new AttackQualityDamageResolver(new DamageMultipliers(0, 0.5m, 1, 2))),
            new DefenseStep(new DefenseResolver(new HitMagnitudeArmorFormula())),
            new ApplyNewEffectsStep(new CombatEffectResolver(new ResolutionEffectInstanceIdFactory()))
        ]);
    }

    private sealed class UnrelatedDiceEffect : ICombatEffect
    {
        /// <inheritdoc />
        public CombatContext Apply(CombatContext context, IDiceRoller dice)
        {
            dice.Roll(new(1, 20));
            return context;
        }
    }

    private sealed class RuneDamageEffect : ICombatEffect
    {
        /// <inheritdoc />
        public CombatContext Apply(CombatContext context, IDiceRoller dice)
        {
            var previous = context.BeforeDefenses!;
            var component = new DamageComponent(new(1, 8), 2, DamageType.Slashing);
            var bonus = new DamageComponentRoll(component, dice.Roll(component.Dice));
            var packet = new DamagePacket(previous.Damage.Components.Select((amount, index) =>
                new DamageAmount(amount.Type, index == 0 ? checked(amount.Amount + bonus.Amount) : amount.Amount)).ToArray());
            var step = new DamageStep(DamageStepType.AttackerModifier, previous.Damage, packet,
                origin: "fixture:rune", flatBonus: 2, componentRolls: [bonus]);
            return context with
            {
                BeforeDefenses = new(new(previous.Breakdown.Steps.Append(step).ToArray()), packet)
            };
        }
    }

    private sealed class NoReductions : IResourceCostModifierProvider
    {
        /// <inheritdoc />
        public IReadOnlyList<ResourceCostReduction> GetReductions(CombatantSnapshot source) => [];
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
            var results = Enumerable.Range(0, formula.Count).Select(_ => _values.Dequeue()).ToArray();
            var roll = new DiceRoll(formula, results, results.Sum());
            _rolls.Add(roll);
            return roll;
        }
    }
}
