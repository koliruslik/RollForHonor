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

/// <summary>Seeded automated duel using real Application launches/impacts and tool-local turn rules.</summary>
internal static class TextCombatScenario
{
    internal static void Run(string[] args)
    {
        var options = DiagnosticOptions.Parse(args);
        using var jsonl = options.OpenWriter();
        var diagnostics = new LoggerCombatDiagnostics(new ConsoleDiagnosticLogger(), options.View, options.Filter, jsonl);
        var warrior = new CombatantSnapshot(new(new Guid("00000000-0000-0000-0000-000000000001")),
            65, 65, 25, 14, [new(DamageType.Fire, 0.2m)], []);
        var mage = new CombatantSnapshot(new(new Guid("00000000-0000-0000-0000-000000000002")),
            55, 55, 8, 15, [], []);
        var state = new BattleState(warrior, mage);
        var random = new Random(options.Seed);
        var impacts = new CombatImpactService(state, new CombatSnapshotBuilder(), new BattlePipeline(),
            () => ScenarioDice.Seeded(random), diagnostics);
        var starts = new AttackInitiationService(state, new ResourceCostResolver(new AdditiveResourceCostPolicy()),
            new BattleCostModifiers(), impacts, () => state.Now, diagnostics);
        var slash = Attack("heavy-slash", DamageType.Slashing, 2, 6, 3, 3, BattleState.Stamina, 8, 2);
        var fire = Attack("fire-bolt", DamageType.Fire, 3, 4, 2, 2, BattleState.Mana, 10, 2);
        var basic = Attack("basic-strike", DamageType.Bludgeoning, 1, 4, 1, 0, BattleState.Stamina, 0, 0);
        var rounds = 0;
        System.Console.Error.WriteLine($"BATTLE seed={options.Seed}; maxRounds={options.Rounds}; Warrior vs Mage");
        for (var round = 1; round <= options.Rounds; round++)
        {
            rounds = round;
            System.Console.Error.WriteLine($"ROUND {round}; time={state.Now.TotalSeconds:0}s");
            Turn(warrior.Id, mage.Id, "Warrior", slash);
            if (state.Get(mage.Id).IsDefeated) break;
            Turn(mage.Id, warrior.Id, "Mage", fire);
            if (state.Get(warrior.Id).IsDefeated) break;
            state.Now += TimeSpan.FromSeconds(1);
        }
        var winner = state.Get(warrior.Id).IsDefeated ? "Mage" : state.Get(mage.Id).IsDefeated ? "Warrior" : "draw (round limit)";
        System.Console.Error.WriteLine($"BATTLE END: {winner}; rounds={rounds}; impacts={state.Impacts}; launches={state.Launches}");
        Status();

        void Turn(CombatantId source, CombatantId target, string name, AttackDefinition preferred)
        {
            var request = Guid.NewGuid();
            var started = starts.Start(request, source, preferred);
            var chosen = preferred;
            if (started.Status is AttackStartStatus.OnCooldown or AttackStartStatus.InsufficientResources)
            {
                System.Console.Error.WriteLine($"{name}: {preferred.Key} unavailable ({started.Status}); use basic strike.");
                started = starts.Start(Guid.NewGuid(), source, basic);
                chosen = basic;
            }
            if (started.Status != AttackStartStatus.Started || started.Attack is null)
                throw new InvalidOperationException($"Unexpected battle launch result: {started.Status}.");
            var impact = started.Attack.Resolve(target);
            if (!impact.IsCommitted) throw new InvalidOperationException("The single-threaded duel must commit every hit calculation.");
            var resolved = (ResolvedCombat)impact.Outcome;
            System.Console.Error.WriteLine($"{name}: {chosen.Key} -> {resolved.Resolution.Result.Target.Outcome}; " +
                $"damage={resolved.Resolution.Result.Target.DamageResolution!.AfterDefenses.Sum()}");
            Status();
        }

        void Status() => System.Console.Error.WriteLine(
            $"Warrior HP={state.Get(warrior.Id).Health}/65 stamina={state.Balance(warrior.Id, BattleState.Stamina)}; " +
            $"Mage HP={state.Get(mage.Id).Health}/55 mana={state.Balance(mage.Id, BattleState.Mana)}");
    }

    private static AttackDefinition Attack(string key, DamageType type, int count, int sides, int flat,
        int attackModifier, ResourceId resource, decimal cost, int cooldown) => new(key,
        new(new(1, 20), attackModifier, NaturalOutcomeShiftRules.Standard,
            [new(new(count, sides), flat, type)], [], type == DamageType.Fire ? AttackTags.Projectile : AttackTags.Melee),
        TimeSpan.FromSeconds(cooldown), [new(resource, cost)]);

    private sealed class BattlePipeline : ICombatPipelineFactory
    {
        /// <inheritdoc />
        public ICombatStep Create(AttackInstance attack, CombatStateSnapshot snapshot) => new CombatPipeline([
            new AttackRollStep(new AttackRollResolver()), new AttackOutcomeStep(new D20AttackOutcomePolicy()),
            new RawDamageStep(new RawDamageResolver()),
            new AttackQualityStep(new AttackQualityDamageResolver(new DamageMultipliers(0, 0.5m, 1, 2))),
            new DefenseStep(new DefenseResolver(new HitMagnitudeArmorFormula()))
        ]);
    }

    private sealed class BattleCostModifiers : IResourceCostModifierProvider
    {
        /// <inheritdoc />
        public IReadOnlyList<ResourceCostReduction> GetReductions(CombatantSnapshot source) => [];
    }

    private sealed class BattleState : ICombatStateAdapter, IAttackInitiationAdapter
    {
        internal static readonly ResourceId Mana = new("mana");
        internal static readonly ResourceId Stamina = new("stamina");
        private Dictionary<CombatantId, CombatantSnapshot> _actors;
        private Dictionary<(CombatantId, ResourceId), decimal> _resources;
        private readonly Dictionary<(CombatantId, string), TimeSpan> _cooldowns = [];
        private readonly Dictionary<Guid, (CombatantId Source, string Key, Guid Attack)> _starts = [];
        private readonly HashSet<Guid> _impacts = [];
        private long _version;
        internal TimeSpan Now { get; set; }
        internal int Impacts => _impacts.Count;
        internal int Launches => _starts.Count;

        internal BattleState(CombatantSnapshot warrior, CombatantSnapshot mage)
        {
            _actors = new[] { warrior, mage }.ToDictionary(actor => actor.Id);
            _resources = new() { [(warrior.Id, Stamina)] = 24, [(mage.Id, Mana)] = 30 };
        }

        internal CombatantSnapshot Get(CombatantId id) => _actors[id];
        internal decimal Balance(CombatantId id, ResourceId resource) => _resources.GetValueOrDefault((id, resource));

        /// <inheritdoc />
        public CombatStateRead Read(CombatantId sourceId, CombatantId targetId) => new(_version, Get(sourceId), Get(targetId));

        /// <inheritdoc />
        public CombatCommitStatus TryCommit(CombatResolution resolution)
        {
            if (_impacts.Contains(resolution.ResolutionId)) return CombatCommitStatus.AlreadyApplied;
            if (resolution.BasedOnStateVersion != _version) return CombatCommitStatus.VersionConflict;
            var staged = new Dictionary<CombatantId, CombatantSnapshot>(_actors);
            foreach (var change in resolution.Changes)
            {
                switch (change)
                {
                    case HealthChanged health:
                        var previous = staged[health.CombatantId];
                        if (previous.Health != health.PreviousHealth) throw new InvalidOperationException("Unexpected health proposal.");
                        staged[previous.Id] = new(previous.Id, health.CurrentHealth, previous.MaxHealth,
                            previous.Armor, previous.Evasion, previous.Resistances, previous.Effects);
                        break;
                    case CombatantDefeated:
                        break;
                    default:
                        throw new InvalidOperationException("The text duel fixture supports health/defeat changes only.");
                }
            }
            _actors = staged;
            _impacts.Add(resolution.ResolutionId);
            _version++;
            return CombatCommitStatus.Applied;
        }

        /// <inheritdoc />
        public AttackInitiationState ReadForStart(CombatantId sourceId, string definitionKey, Guid requestId)
        {
            Guid? attack = null;
            if (_starts.TryGetValue(requestId, out var receipt))
            {
                if (receipt.Source != sourceId || receipt.Key != definitionKey)
                    throw new InvalidOperationException("Request identity reused for another launch.");
                attack = receipt.Attack;
            }
            var resources = _resources.Where(pair => pair.Key.Item1 == sourceId)
                .Select(pair => new ResourceAmount(pair.Key.Item2, pair.Value)).ToArray();
            return new(_version, Get(sourceId), resources, _cooldowns.GetValueOrDefault((sourceId, definitionKey)), attack);
        }

        /// <inheritdoc />
        public CombatCommitStatus TryStart(AttackStartProposal proposal)
        {
            if (_starts.ContainsKey(proposal.RequestId)) return CombatCommitStatus.AlreadyApplied;
            if (proposal.ExpectedVersion != _version) return CombatCommitStatus.VersionConflict;
            var staged = new Dictionary<(CombatantId, ResourceId), decimal>(_resources);
            foreach (var cost in proposal.Costs.Costs)
            {
                var key = (proposal.SourceId, cost.Resource);
                var balance = staged.GetValueOrDefault(key);
                if (balance < cost.Amount) throw new InvalidOperationException("Unaffordable battle proposal.");
                staged[key] = balance - cost.Amount;
            }
            _resources = staged;
            _cooldowns[(proposal.SourceId, proposal.DefinitionKey)] = proposal.ReadyAt;
            _starts.Add(proposal.RequestId, (proposal.SourceId, proposal.DefinitionKey, proposal.AttackId));
            _version++;
            return CombatCommitStatus.Applied;
        }
    }
}
