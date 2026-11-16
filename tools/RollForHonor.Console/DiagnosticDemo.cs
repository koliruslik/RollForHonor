using RollForHonor.Application.Combat.Models;
using RollForHonor.Application.Combat.Ports;
using RollForHonor.Application.Combat.Services;
using RollForHonor.Domain.Combat.Attacks.Models;
using RollForHonor.Domain.Combat.Attacks.Services;
using RollForHonor.Domain.Combat.Damage.Configuration;
using RollForHonor.Domain.Combat.Damage.Models;
using RollForHonor.Domain.Combat.Damage.Services;
using RollForHonor.Domain.Combat.Resolution.Models;
using RollForHonor.Domain.Combat.Resolution.Services;
using RollForHonor.Domain.Combat.Resolution.Services.Steps;
using RollForHonor.Domain.Combat.State.Changes;
using RollForHonor.Domain.Combat.State.Models;
using RollForHonor.Domain.Resources.Models;
using RollForHonor.Domain.Resources.Services;
using RollForHonor.Infrastructure.Diagnostics;

namespace RollForHonor.ConsoleHost;

/// <summary>Explicit tool fixtures, not the game's runtime storage or balance rules.</summary>
internal static class DiagnosticDemo
{
    /// <summary>Runs fixed launch/impact fixtures with the requested diagnostic view.</summary>
    /// <param name="args">Command and output/filter arguments.</param>
    public static void Run(string[] args)
    {
        var options = DiagnosticOptions.Parse(args);
        using var jsonl = options.OpenWriter();
        var diagnostics = new LoggerCombatDiagnostics(new ConsoleDiagnosticLogger(), options.View, options.Filter, jsonl);
        var state = new DemoState();
        var rolls = new Queue<int>([1, 6, 15, 20, 15]);
        var impacts = new CombatImpactService(state, new CombatSnapshotBuilder(), new DemoPipeline(),
            () => ScenarioDice.Scripted(rolls.Dequeue(), 2, 5, 3), diagnostics);
        var starts = new AttackInitiationService(state, new ResourceCostResolver(new AdditiveResourceCostPolicy()),
            new DemoReductions(), impacts, () => TimeSpan.Zero, diagnostics);
        var payload = new AttackPayload(new(1, 20), 0, NaturalOutcomeShiftRules.Standard,
            [new(new(2, 6), 3, DamageType.Slashing), new(new(1, 4), 0, DamageType.Fire)], [], AttackTags.Projectile);
        var definition = new AttackDefinition("diagnostic-shot", payload, TimeSpan.FromSeconds(2), [new(DemoState.Mana, 20)]);
        var attack = starts.Start(Guid.NewGuid(), state.Source.Id, definition).Attack!;
        for (var i = 0; i < 4; i++) attack.Resolve(state.Target.Id);
        state.Conflict = true;
        attack.Resolve(state.Target.Id);
        starts.Start(Guid.NewGuid(), state.Source.Id, definition);
    }

    /// <summary>Exercises contact admission and termination with real launches and impacts.</summary>
    /// <param name="args">Command and output/filter arguments for the fixed fixtures.</param>
    public static void RunContacts(string[] args)
    {
        var options = DiagnosticOptions.Parse(args);
        using var jsonl = options.OpenWriter();
        var diagnostics = new LoggerCombatDiagnostics(new ConsoleDiagnosticLogger(), options.View, options.Filter, jsonl);
        (string Name, AttackExecutionRules Rules, int Roll, AttackContactStatus Repeat,
            AttackEndReason End, int Health)[] cases =
        [
            ("piercing", new(AttackRepeatPolicy.OncePerTarget, AttackStopCondition.EnvironmentContact),
                15, AttackContactStatus.TargetAlreadyContacted, AttackEndReason.EnvironmentContact, 91),
            ("first-contact", new(stopOn: AttackStopCondition.CombatantContact | AttackStopCondition.EnvironmentContact),
                15, AttackContactStatus.AttackEnded, AttackEndReason.CombatantContact, 91),
            ("repeat", new(maxLifetime: TimeSpan.FromSeconds(1)),
                15, AttackContactStatus.Processed, AttackEndReason.LifetimeExpired, 82),
            ("animation", new(AttackRepeatPolicy.OncePerTarget),
                15, AttackContactStatus.TargetAlreadyContacted, AttackEndReason.AnimationFinished, 91),
            ("evaded-once", new(AttackRepeatPolicy.OncePerTarget, maxLifetime: TimeSpan.FromSeconds(1)),
                1, AttackContactStatus.TargetAlreadyContacted, AttackEndReason.LifetimeExpired, 100)
        ];
        foreach (var scenario in cases)
        {
            var now = TimeSpan.Zero;
            var state = new DemoState();
            var diceCount = 0;
            var impacts = new CombatImpactService(state, new CombatSnapshotBuilder(), new DemoPipeline(), () =>
            {
                diceCount++;
                return ScenarioDice.Scripted(scenario.Roll, 2, 5, 3);
            }, diagnostics);
            var starts = new AttackInitiationService(state, new ResourceCostResolver(new AdditiveResourceCostPolicy()),
                new DemoReductions(), impacts, () => now, diagnostics);
            var payload = new AttackPayload(new(1, 20), 0, NaturalOutcomeShiftRules.Standard,
                [new(new(2, 6), 3, DamageType.Slashing), new(new(1, 4), 0, DamageType.Fire)], [], AttackTags.Projectile);
            var definition = new AttackDefinition("contact-demo", payload, TimeSpan.FromSeconds(2),
                [new(DemoState.Mana, 20)], scenario.Rules);
            var attack = starts.Start(Guid.NewGuid(), state.Source.Id, definition).Attack!;
            var first = attack.ResolveContact(state.Target.Id);
            var repeated = attack.ResolveContact(state.Target.Id);
            var obstacle = attack.ContactEnvironment();
            if (scenario.Name == "animation") attack.End(AttackEndReason.AnimationFinished);
            now = TimeSpan.FromSeconds(1);
            attack.UpdateLifetime();
            var ended = attack.ResolveContact(state.Target.Id);
            var expectedDice = scenario.Repeat == AttackContactStatus.Processed ? 2 : 1;
            if (first.Status != AttackContactStatus.Processed || repeated.Status != scenario.Repeat
                || attack.EndReason != scenario.End || ended.Status != AttackContactStatus.AttackEnded
                || diceCount != expectedDice || state.Target.Health != scenario.Health)
                throw new InvalidOperationException($"Contact scenario failed: {scenario.Name}.");
            System.Console.Error.WriteLine($"PASS contacts/{scenario.Name}: first={first.Status}; repeat={repeated.Status}; " +
                $"obstacle={obstacle.Status}; end={attack.EndReason}; calculations={diceCount}; targetHP={state.Target.Health}");
        }
    }

    private sealed class DemoPipeline : ICombatPipelineFactory
    {
        /// <inheritdoc />
        public ICombatStep Create(AttackInstance attack, CombatStateSnapshot snapshot) => new CombatPipeline([
            new AttackRollStep(new AttackRollResolver()), new AttackOutcomeStep(new D20AttackOutcomePolicy()),
            new RawDamageStep(new RawDamageResolver()),
            new AttackQualityStep(new AttackQualityDamageResolver(new DamageMultipliers(0, 0.5m, 1, 2))),
            new DefenseStep(new DefenseResolver(new HitMagnitudeArmorFormula()))
        ]);
    }

    private sealed class DemoReductions : IResourceCostModifierProvider
    {
        /// <inheritdoc />
        public IReadOnlyList<ResourceCostReduction> GetReductions(CombatantSnapshot source) =>
            [new(DemoState.Mana, 0.1m, "demo:ten-percent"), new(DemoState.Mana, 0.2m, "demo:twenty-percent")];
    }

    private sealed class DemoState : ICombatStateAdapter, IAttackInitiationAdapter
    {
        internal static readonly ResourceId Mana = new("mana");
        internal CombatantSnapshot Source { get; } = new(new(new Guid("00000000-0000-0000-0000-000000000001")), 100, 100, 0, 15, [], []);
        internal CombatantSnapshot Target { get; private set; } = new(new(new Guid("00000000-0000-0000-0000-000000000002")),
            100, 100, 30, 15, [new(DamageType.Fire, 0.5m)], []);
        internal bool Conflict { get; set; }
        private long _version;
        private decimal _mana = 100;
        private TimeSpan _readyAt;
        private readonly Dictionary<Guid, Guid> _starts = [];
        /// <inheritdoc />
        public CombatStateRead Read(CombatantId sourceId, CombatantId targetId) => new(_version, Source, Target);
        /// <inheritdoc />
        public CombatCommitStatus TryCommit(CombatResolution resolution)
        {
            if (Conflict || resolution.BasedOnStateVersion != _version) return CombatCommitStatus.VersionConflict;
            var change = resolution.Changes.OfType<HealthChanged>().SingleOrDefault();
            if (change is not null) Target = new(Target.Id, change.CurrentHealth, Target.MaxHealth,
                Target.Armor, Target.Evasion, Target.Resistances, Target.Effects);
            _version++;
            return CombatCommitStatus.Applied;
        }
        /// <inheritdoc />
        public AttackInitiationState ReadForStart(CombatantId sourceId, string definitionKey, Guid requestId) =>
            new(_version, Source, [new(Mana, _mana)], _readyAt, _starts.TryGetValue(requestId, out var id) ? id : null);
        /// <inheritdoc />
        public CombatCommitStatus TryStart(AttackStartProposal proposal)
        {
            if (_starts.ContainsKey(proposal.RequestId)) return CombatCommitStatus.AlreadyApplied;
            if (proposal.ExpectedVersion != _version) return CombatCommitStatus.VersionConflict;
            _mana -= proposal.Costs.Costs.Single().Amount;
            _readyAt = proposal.ReadyAt;
            _starts.Add(proposal.RequestId, proposal.AttackId);
            _version++;
            return CombatCommitStatus.Applied;
        }
    }

}
