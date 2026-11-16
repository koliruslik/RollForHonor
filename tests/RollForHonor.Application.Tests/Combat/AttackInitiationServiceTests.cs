using RollForHonor.Application.Combat.Models;
using RollForHonor.Application.Combat.Ports;
using RollForHonor.Application.Combat.Services;
using RollForHonor.Domain.Combat.Attacks.Models;
using RollForHonor.Domain.Combat.Attacks.Services;
using RollForHonor.Domain.Combat.Damage.Configuration;
using RollForHonor.Domain.Combat.Damage.Models;
using RollForHonor.Domain.Combat.Damage.Services;
using RollForHonor.Domain.Combat.Effects.Models;
using RollForHonor.Domain.Combat.Resolution.Models;
using RollForHonor.Domain.Combat.Resolution.Services;
using RollForHonor.Domain.Combat.Resolution.Services.Steps;
using RollForHonor.Domain.Combat.State.Models;
using RollForHonor.Domain.Dice.Models;
using RollForHonor.Domain.Dice.Services;
using RollForHonor.Domain.Resources.Models;
using RollForHonor.Domain.Resources.Services;
using RollForHonor.Domain.Tests.Diagnostics;
using Xunit.Abstractions;

namespace RollForHonor.Application.Tests.Combat;

public sealed class AttackInitiationServiceTests : CombatDiagnosticTestBase
{
    private readonly ITestOutputHelper _output;

    public AttackInitiationServiceTests(ITestOutputHelper output) : base(output) => _output = output;

    private static readonly ResourceId Mana = new("mana");
    private static readonly ResourceId Stamina = new("stamina");

    [Fact]
    public void AcceptedLaunch_ChargesOnceAndStartsCooldown_BeforeAnyImpactIsCalculated()
    {
        var rig = new Rig();
        var before = rig.State.Version;
        var result = Start(rig, Guid.NewGuid(), rig.Source.Id, Definition());
        Assert.Equal(AttackStartStatus.Started, result.Status);
        var attack = Assert.IsType<AttackInstance>(result.Attack);
        Assert.Equal(attack.Id, result.AttackId);
        Assert.Equal(80m, rig.State.Balance(rig.Source.Id, Mana));
        Assert.Equal(40m, rig.State.Balance(rig.Source.Id, Stamina));
        Assert.Equal(TimeSpan.FromSeconds(2), rig.State.ReadyAt(rig.Source.Id, "shot"));
        Assert.Equal(before + 1, rig.State.Version);
        Assert.Equal(0, rig.DiceCreated);
        Assert.Equal(50, rig.State.Get(rig.Target.Id).Health);

        for (var i = 0; i < 2; i++)
        {
            var impact = attack.Resolve(rig.Target.Id);
            LogOutcome(impact.Outcome);
            Assert.True(impact.IsCommitted);
            Assert.Equal(attack.Id, impact.AttackId);
        }
        Assert.Equal(38, rig.State.Get(rig.Target.Id).Health);
        Assert.Equal(80m, rig.State.Balance(rig.Source.Id, Mana));
        Assert.Equal(40m, rig.State.Balance(rig.Source.Id, Stamina));
        Assert.Single(rig.State.Started);
        Assert.Equal(2, rig.DiceCreated);
    }

    [Fact]
    public void MissAfterLaunch_DoesNotRefundOrChargeLaunchCostsAgain()
    {
        var rig = new Rig { Roll = 1 };
        var started = Start(rig, Guid.NewGuid(), rig.Source.Id, Definition());
        var impact = started.Attack!.Resolve(rig.Target.Id);
        LogOutcome(impact.Outcome);
        var resolution = Assert.IsType<ResolvedCombat>(impact.Outcome).Resolution;
        Assert.Equal(AttackOutcome.Evaded, resolution.Result.Target.Outcome);
        Assert.Equal(0, resolution.Result.Target.DamageResolution!.AfterDefenses.Sum());
        Assert.Equal(50, rig.State.Get(rig.Target.Id).Health);
        Assert.Equal(80m, rig.State.Balance(rig.Source.Id, Mana));
        Assert.Single(rig.State.Started);
    }

    [Fact]
    public void CurrentSourceBuffsAddAtLaunch_AndAffectOnlyTheirOwnResources()
    {
        var rig = new Rig();
        rig.State.ExternalUpdate(WithEffects(rig.Source,
            Reduction(Mana, 0.1m), Reduction(Mana, 0.2m), Reduction(Stamina, 0.5m)));
        var result = Start(rig, Guid.NewGuid(), rig.Source.Id, Definition());
        Assert.Equal(AttackStartStatus.Started, result.Status);
        Assert.Equal(new[] { 14m, 5m }, result.Costs!.Costs.Select(value => value.Amount));
        Assert.Equal(86m, rig.State.Balance(rig.Source.Id, Mana));
        Assert.Equal(45m, rig.State.Balance(rig.Source.Id, Stamina));
        Assert.Equal(3, rig.Modifiers.LastSource!.Effects.Count);
    }

    [Fact]
    public void InsufficientOneResource_ChangesNoResourceCooldownOrRequestReceipt()
    {
        var rig = new Rig();
        rig.State.SetResources(rig.Source.Id, new ResourceAmount(Mana, 100), new ResourceAmount(Stamina, 9));
        var before = rig.State.Version;
        var request = Guid.NewGuid();
        var result = Start(rig, request, rig.Source.Id, Definition());
        Assert.Equal(AttackStartStatus.InsufficientResources, result.Status);
        Assert.Null(result.Attack);
        Assert.Equal(100m, rig.State.Balance(rig.Source.Id, Mana));
        Assert.Equal(9m, rig.State.Balance(rig.Source.Id, Stamina));
        Assert.Equal(TimeSpan.Zero, rig.State.ReadyAt(rig.Source.Id, "shot"));
        Assert.Equal(before, rig.State.Version);
        Assert.Equal(0, rig.State.StartAttempts);
        Assert.Null(rig.State.ReadForStart(rig.Source.Id, "shot", request).StartedAttackId);
    }

    [Fact]
    public void CooldownRejectsNewLaunch_AndExactDeadlineAllowsIt()
    {
        var rig = new Rig();
        var definition = Definition();
        Assert.Equal(AttackStartStatus.Started, Start(rig, Guid.NewGuid(), rig.Source.Id, definition).Status);
        rig.Now = TimeSpan.FromSeconds(1.999);
        var before = rig.State.Version;
        var rejected = Start(rig, Guid.NewGuid(), rig.Source.Id, definition);
        Assert.Equal(AttackStartStatus.OnCooldown, rejected.Status);
        Assert.Null(rejected.Attack);
        Assert.Equal(80m, rig.State.Balance(rig.Source.Id, Mana));
        Assert.Equal(before, rig.State.Version);
        Assert.Equal(1, rig.State.StartAttempts);

        rig.Now = TimeSpan.FromSeconds(2);
        Assert.Equal(AttackStartStatus.Started, Start(rig, Guid.NewGuid(), rig.Source.Id, definition).Status);
        Assert.Equal(60m, rig.State.Balance(rig.Source.Id, Mana));
        Assert.Equal(TimeSpan.FromSeconds(4), rig.State.ReadyAt(rig.Source.Id, "shot"));
    }

    [Fact]
    public void ReplayedCommandReturnsOriginalIdentity_WithoutAnotherChargeOrSpawnableAttack()
    {
        var rig = new Rig();
        var request = Guid.NewGuid();
        var definition = Definition();
        var first = Start(rig, request, rig.Source.Id, definition);
        var before = rig.State.Version;
        var replay = Start(rig, request, rig.Source.Id, definition);
        Assert.Equal(AttackStartStatus.AlreadyStarted, replay.Status);
        Assert.Equal(first.AttackId, replay.AttackId);
        Assert.Null(replay.Attack);
        Assert.Equal(80m, rig.State.Balance(rig.Source.Id, Mana));
        Assert.Equal(before, rig.State.Version);
        Assert.Equal(1, rig.State.StartAttempts);
        Assert.Equal(1, rig.Modifiers.Calls);
    }

    [Fact]
    public void StaleInitiation_DoesNotDeductOrStartCooldown_AndDoesNotRetryAutomatically()
    {
        var rig = new Rig();
        rig.State.BeforeStart = _ =>
        {
            rig.State.BeforeStart = null;
            rig.State.ExternalUpdate(WithEffects(rig.Source, Reduction(Mana, 0.5m)));
        };
        var request = Guid.NewGuid();
        var result = Start(rig, request, rig.Source.Id, Definition());
        Assert.Equal(AttackStartStatus.VersionConflict, result.Status);
        Assert.Null(result.Attack);
        Assert.Equal(100m, rig.State.Balance(rig.Source.Id, Mana));
        Assert.Equal(50m, rig.State.Balance(rig.Source.Id, Stamina));
        Assert.Equal(TimeSpan.Zero, rig.State.ReadyAt(rig.Source.Id, "shot"));
        Assert.Empty(rig.State.Started);
        Assert.Equal(1, rig.State.StartAttempts);
        Assert.Equal(1, rig.Modifiers.Calls);

        var retry = Start(rig, request, rig.Source.Id, Definition());
        Assert.Equal(AttackStartStatus.Started, retry.Status);
        Assert.Equal(90m, rig.State.Balance(rig.Source.Id, Mana));
        Assert.Single(rig.State.Started);
    }

    [Fact]
    public void AdapterFailureAfterStaging_LeavesAllBalancesCooldownAndReceiptUnchanged()
    {
        var rig = new Rig();
        rig.State.FailStartAfterStaging = true;
        var request = Guid.NewGuid();
        var before = rig.State.Version;
        Assert.Throws<InvalidOperationException>(() => Start(rig, request, rig.Source.Id, Definition()));
        Assert.Equal(100m, rig.State.Balance(rig.Source.Id, Mana));
        Assert.Equal(50m, rig.State.Balance(rig.Source.Id, Stamina));
        Assert.Equal(TimeSpan.Zero, rig.State.ReadyAt(rig.Source.Id, "shot"));
        Assert.Equal(before, rig.State.Version);
        Assert.Empty(rig.State.Started);
        Assert.Null(rig.State.ReadForStart(rig.Source.Id, "shot", request).StartedAttackId);

        rig.State.FailStartAfterStaging = false;
        Assert.Equal(AttackStartStatus.Started, Start(rig, request, rig.Source.Id, Definition()).Status);
        Assert.Equal(80m, rig.State.Balance(rig.Source.Id, Mana));
    }

    [Fact]
    public void DefeatedSourceCannotStart_AndNoCostModifiersOrCommitAreInvoked()
    {
        var rig = new Rig();
        rig.State.ExternalUpdate(new CombatantSnapshot(rig.Source.Id, 0, 100, 0, 15, [], []));
        var before = rig.State.Version;
        var result = Start(rig, Guid.NewGuid(), rig.Source.Id, Definition());
        Assert.Equal(AttackStartStatus.SourceDefeated, result.Status);
        Assert.Null(result.Attack);
        Assert.Equal(0, rig.Modifiers.Calls);
        Assert.Equal(0, rig.State.StartAttempts);
        Assert.Equal(before, rig.State.Version);
        Assert.Equal(100m, rig.State.Balance(rig.Source.Id, Mana));
    }

    [Fact]
    public void FullDiscountAllowsLaunchWithNoBalance_AndNeverCreatesResources()
    {
        var rig = new Rig();
        rig.State.SetResources(rig.Source.Id);
        rig.State.ExternalUpdate(WithEffects(rig.Source, Reduction(Mana, 0.6m), Reduction(Mana, 0.6m)));
        var definition = new AttackDefinition("free", Payload(), TimeSpan.Zero, [new ResourceAmount(Mana, 20)]);
        var result = Start(rig, Guid.NewGuid(), rig.Source.Id, definition);
        Assert.Equal(AttackStartStatus.Started, result.Status);
        Assert.Equal(0m, Assert.Single(result.Costs!.Costs).Amount);
        Assert.Equal(0m, rig.State.Balance(rig.Source.Id, Mana));
        Assert.NotNull(result.Attack);
    }

    [Fact]
    public void ExactBalanceAndFractionalCostAreDeductedExactly()
    {
        var rig = new Rig();
        rig.State.SetResources(rig.Source.Id, new ResourceAmount(Mana, 7.7m));
        rig.State.ExternalUpdate(WithEffects(rig.Source, Reduction(Mana, 0.3m)));
        var definition = new AttackDefinition("fractional", Payload(), TimeSpan.Zero, [new ResourceAmount(Mana, 11)]);
        var result = Start(rig, Guid.NewGuid(), rig.Source.Id, definition);
        Assert.Equal(AttackStartStatus.Started, result.Status);
        Assert.Equal(7.7m, Assert.Single(result.Costs!.Costs).Amount);
        Assert.Equal(0m, rig.State.Balance(rig.Source.Id, Mana));
    }

    [Fact]
    public void CooldownsAreIndependentByDefinitionKey()
    {
        var rig = new Rig();
        Assert.Equal(AttackStartStatus.Started, Start(rig, Guid.NewGuid(), rig.Source.Id, Definition()).Status);
        var other = new AttackDefinition("other", Payload(), TimeSpan.FromSeconds(5), []);
        Assert.Equal(AttackStartStatus.Started, Start(rig, Guid.NewGuid(), rig.Source.Id, other).Status);
        Assert.Equal(TimeSpan.FromSeconds(2), rig.State.ReadyAt(rig.Source.Id, "shot"));
        Assert.Equal(TimeSpan.FromSeconds(5), rig.State.ReadyAt(rig.Source.Id, "other"));
    }

    [Fact]
    public void ReceiptAppearingBeforeCommit_DoesNotChargeOrExposeAnotherAttack()
    {
        var rig = new Rig();
        var request = Guid.NewGuid();
        var before = rig.State.Version;
        rig.State.BeforeStart = proposal =>
        {
            rig.State.BeforeStart = null;
            Assert.Equal(CombatCommitStatus.Applied, rig.State.TryStart(proposal));
        };
        var result = Start(rig, request, rig.Source.Id, Definition());
        Assert.Equal(AttackStartStatus.AlreadyStarted, result.Status);
        Assert.Null(result.Attack);
        Assert.Equal(80m, rig.State.Balance(rig.Source.Id, Mana));
        Assert.Equal(40m, rig.State.Balance(rig.Source.Id, Stamina));
        Assert.Equal(before + 1, rig.State.Version);
        var accepted = Assert.Single(rig.State.Started);
        var replay = Start(rig, request, rig.Source.Id, Definition());
        Assert.Equal(accepted.AttackId, replay.AttackId);
        Assert.Null(replay.Attack);
    }

    [Fact]
    public void SameDefinitionHasIndependentCooldownAndBalancesForEachSource()
    {
        var rig = new Rig();
        rig.State.SetResources(rig.Target.Id, new ResourceAmount(Mana, 100), new ResourceAmount(Stamina, 50));
        Assert.Equal(AttackStartStatus.Started, Start(rig, Guid.NewGuid(), rig.Source.Id, Definition()).Status);
        var other = Start(rig, Guid.NewGuid(), rig.Target.Id, Definition());
        Assert.Equal(AttackStartStatus.Started, other.Status);
        Assert.Equal(80m, rig.State.Balance(rig.Source.Id, Mana));
        Assert.Equal(80m, rig.State.Balance(rig.Target.Id, Mana));
        Assert.Equal(TimeSpan.FromSeconds(2), rig.State.ReadyAt(rig.Source.Id, "shot"));
        Assert.Equal(TimeSpan.FromSeconds(2), rig.State.ReadyAt(rig.Target.Id, "shot"));
        Assert.Equal(2, rig.State.Started.Count);
    }

    [Fact]
    public void EmptyRequestIdentityIsRejectedBeforeAnyLaunchSideEffects()
    {
        var rig = new Rig();
        var before = rig.State.Version;
        Assert.Throws<ArgumentException>(() => Start(rig, Guid.Empty, rig.Source.Id, Definition()));
        Assert.Equal(before, rig.State.Version);
        Assert.Equal(0, rig.Modifiers.Calls);
        Assert.Equal(0, rig.State.StartAttempts);
    }

    private AttackStartResult Start(Rig rig, Guid requestId, CombatantId sourceId, AttackDefinition definition)
    {
        var result = rig.Service.Start(requestId, sourceId, definition);
        if (CombatDiagnosticOutput.IsEnabled(Environment.GetEnvironmentVariable("RFH_COMBAT_DIAGNOSTICS")))
        {
            _output.WriteLine($"ATTACK START: {result.Status}; attack={result.AttackId?.ToString() ?? "none"}");
            if (result.Costs is { } costs)
                for (var index = 0; index < costs.Costs.Count; index++)
                    _output.WriteLine(FormattableString.Invariant(
                        $"  {costs.Costs[index].Resource.Value}: {costs.BaseCosts[index].Amount} -> {costs.Costs[index].Amount}"));
        }
        return result;
    }

    private static AttackDefinition Definition() => new("shot", Payload(), TimeSpan.FromSeconds(2),
        [new ResourceAmount(Mana, 20), new ResourceAmount(Stamina, 10)]);
    private static AttackPayload Payload() => new(new DiceFormula(1, 20), 0, NaturalOutcomeShiftRules.Standard,
        [new DamageComponent(new DiceFormula(1, 6), 3, DamageType.Slashing)], [], AttackTags.Projectile);
    private static CombatantSnapshot Character() => new(new CombatantId(Guid.NewGuid()), 50, 100, 0, 15, [], []);
    private static CombatantSnapshot WithEffects(CombatantSnapshot source, params CombatEffectSnapshot[] effects) =>
        new(source.Id, source.Health, source.MaxHealth, source.Armor, source.Evasion, source.Resistances, effects);
    private static CombatEffectSnapshot Reduction(ResourceId resource, decimal fraction) =>
        new(new EffectInstanceId(Guid.NewGuid()), new TestReductionDefinition(new ResourceCostReduction(resource, fraction)));

    private sealed record TestReductionDefinition : CombatEffectDefinition
    {
        public ResourceCostReduction Reduction { get; }
        public TestReductionDefinition(ResourceCostReduction reduction) : base(new EffectDefinitionId(Guid.NewGuid())) => Reduction = reduction;
    }

    private sealed class ModifierProvider : IResourceCostModifierProvider
    {
        public int Calls { get; private set; }
        public CombatantSnapshot? LastSource { get; private set; }
        public IReadOnlyList<ResourceCostReduction> GetReductions(CombatantSnapshot source)
        {
            Calls++;
            LastSource = source;
            return source.Effects.Select(value => value.Definition).OfType<TestReductionDefinition>()
                .Select(value => value.Reduction).ToArray();
        }
    }

    private sealed class Rig
    {
        public CombatantSnapshot Source { get; } = Character();
        public CombatantSnapshot Target { get; } = Character();
        public TestCombatStateAdapter State { get; }
        public ModifierProvider Modifiers { get; } = new();
        public TimeSpan Now { get; set; }
        public int Roll { get; set; } = 15;
        public int DiceCreated { get; private set; }
        public AttackInitiationService Service { get; }

        public Rig()
        {
            State = new TestCombatStateAdapter(Source, Target);
            State.SetResources(Source.Id, new ResourceAmount(Mana, 100), new ResourceAmount(Stamina, 50));
            var impacts = new CombatImpactService(State, new CombatSnapshotBuilder(), new PipelineFactory(), () =>
            {
                DiceCreated++;
                return new ScriptedDice(Roll, 3);
            });
            Service = new AttackInitiationService(State, new ResourceCostResolver(new AdditiveResourceCostPolicy()),
                Modifiers, impacts, () => Now);
        }
    }

    private sealed class PipelineFactory : ICombatPipelineFactory
    {
        public ICombatStep Create(AttackInstance attack, CombatStateSnapshot snapshot) => new CombatPipeline([
            new AttackRollStep(new AttackRollResolver()), new AttackOutcomeStep(new D20AttackOutcomePolicy()),
            new RawDamageStep(new RawDamageResolver()),
            new AttackQualityStep(new AttackQualityDamageResolver(new DamageMultipliers(0, 0.5m, 1, 2))),
            new DefenseStep(new DefenseResolver(new HitMagnitudeArmorFormula()))
        ]);
    }

    private sealed class ScriptedDice(params int[] values) : IDiceRoller
    {
        private readonly Queue<int> _values = new(values);
        private readonly List<DiceRoll> _rolls = [];
        public DiceTrace Trace => new(_rolls);
        public DiceRoll Roll(DiceFormula formula)
        {
            var values = Enumerable.Range(0, formula.Count).Select(_ => _values.Dequeue()).ToArray();
            var result = new DiceRoll(formula, values, values.Sum());
            _rolls.Add(result);
            return result;
        }
    }
}
