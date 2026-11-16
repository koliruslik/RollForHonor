using RollForHonor.Application.Combat.Models;
using RollForHonor.Application.Combat.Ports;
using RollForHonor.Domain.Combat.Resolution.Models;
using RollForHonor.Domain.Combat.State.Changes;
using RollForHonor.Domain.Combat.State.Models;
using RollForHonor.Domain.Resources.Models;

namespace RollForHonor.Application.Tests.Combat;

// Test adapter only: does not select the game's runtime storage or concurrency strategy.
internal sealed class TestCombatStateAdapter(params CombatantSnapshot[] characters) : ICombatStateAdapter, IAttackInitiationAdapter
{
    private Dictionary<CombatantId, CombatantSnapshot> _state = characters.ToDictionary(value => value.Id);
    private readonly HashSet<Guid> _applied = [];
    private readonly Dictionary<CombatantId, Dictionary<ResourceId, decimal>> _resources = [];
    private readonly Dictionary<(CombatantId, string), TimeSpan> _cooldowns = [];
    private readonly Dictionary<Guid, (CombatantId Source, string Key, Guid AttackId)> _starts = [];
    public long Version { get; private set; }
    public int CommitAttempts { get; private set; }
    public List<CombatStateRead> Reads { get; } = [];
    public List<CombatResolution> Committed { get; } = [];
    public Action<CombatResolution>? BeforeCommit { get; set; }
    public bool FailAfterStaging { get; set; }
    public Action<AttackStartProposal>? BeforeStart { get; set; }
    public bool FailStartAfterStaging { get; set; }
    public int StartAttempts { get; private set; }
    public List<AttackStartProposal> Started { get; } = [];

    public decimal Balance(CombatantId source, ResourceId resource) =>
        _resources.TryGetValue(source, out var pools) ? pools.GetValueOrDefault(resource) : 0m;

    public TimeSpan ReadyAt(CombatantId source, string key) => _cooldowns.GetValueOrDefault((source, key));

    public void SetResources(CombatantId source, params ResourceAmount[] resources)
    {
        _resources[source] = resources.ToDictionary(value => value.Resource, value => value.Amount);
        Version++;
    }

    public AttackInitiationState ReadForStart(CombatantId sourceId, string definitionKey, Guid requestId)
    {
        Guid? previous = null;
        if (_starts.TryGetValue(requestId, out var receipt))
        {
            if (receipt.Source != sourceId || receipt.Key != definitionKey)
                throw new InvalidOperationException("The launch request identity was reused for another command.");
            previous = receipt.AttackId;
        }
        var resources = _resources.TryGetValue(sourceId, out var balances)
            ? balances.Select(value => new ResourceAmount(value.Key, value.Value)).ToArray() : [];
        return new AttackInitiationState(Version, _state[sourceId], resources, ReadyAt(sourceId, definitionKey), previous);
    }

    public CombatCommitStatus TryStart(AttackStartProposal proposal)
    {
        StartAttempts++;
        BeforeStart?.Invoke(proposal);
        if (_starts.TryGetValue(proposal.RequestId, out var receipt))
        {
            if (receipt.Source != proposal.SourceId || receipt.Key != proposal.DefinitionKey)
                throw new InvalidOperationException("The launch request identity was reused for another command.");
            return CombatCommitStatus.AlreadyApplied;
        }
        if (proposal.ExpectedVersion != Version) return CombatCommitStatus.VersionConflict;
        var balances = _resources.TryGetValue(proposal.SourceId, out var existing)
            ? new Dictionary<ResourceId, decimal>(existing) : [];
        foreach (var cost in proposal.Costs.Costs)
        {
            var available = balances.GetValueOrDefault(cost.Resource);
            if (available < cost.Amount) throw new InvalidOperationException("Unaffordable resource proposal.");
            balances[cost.Resource] = available - cost.Amount;
        }
        if (FailStartAfterStaging) throw new InvalidOperationException("Deliberate initiation failure after staging.");
        var nextVersion = checked(Version + 1);
        _resources[proposal.SourceId] = balances;
        _cooldowns[(proposal.SourceId, proposal.DefinitionKey)] = proposal.ReadyAt;
        _starts.Add(proposal.RequestId, (proposal.SourceId, proposal.DefinitionKey, proposal.AttackId));
        Version = nextVersion;
        Started.Add(proposal);
        return CombatCommitStatus.Applied;
    }

    public CombatantSnapshot Get(CombatantId id) => _state[id];

    public void ExternalUpdate(CombatantSnapshot state)
    {
        _state[state.Id] = state;
        Version++;
    }

    public CombatStateRead Read(CombatantId sourceId, CombatantId targetId)
    {
        var read = new CombatStateRead(Version, _state[sourceId], _state[targetId]);
        Reads.Add(read);
        return read;
    }

    public CombatCommitStatus TryCommit(CombatResolution resolution)
    {
        CommitAttempts++;
        BeforeCommit?.Invoke(resolution);
        if (_applied.Contains(resolution.ResolutionId)) return CombatCommitStatus.AlreadyApplied;
        if (resolution.BasedOnStateVersion != Version) return CombatCommitStatus.VersionConflict;
        var staged = new Dictionary<CombatantId, CombatantSnapshot>(_state);
        foreach (var change in resolution.Changes)
        {
            switch (change)
            {
                case HealthChanged health:
                    var participant = staged[health.CombatantId];
                    if (participant.Health != health.PreviousHealth)
                        throw new InvalidOperationException("Health proposal is stale.");
                    staged[participant.Id] = Copy(participant, health.CurrentHealth, participant.Effects);
                    break;
                case EffectApplied applied:
                    var recipient = staged[applied.CombatantId];
                    if (recipient.Effects.Any(effect => effect.Id == applied.Effect.Id))
                        throw new InvalidOperationException("Effect identity is duplicated.");
                    staged[recipient.Id] = Copy(recipient, recipient.Health, recipient.Effects.Append(applied.Effect).ToArray());
                    break;
                case EffectRemoved removed:
                    var previous = staged[removed.CombatantId];
                    if (!previous.Effects.Any(effect => effect.Id == removed.EffectId))
                        throw new InvalidOperationException("Removed effect does not exist.");
                    staged[previous.Id] = Copy(previous, previous.Health,
                        previous.Effects.Where(effect => effect.Id != removed.EffectId).ToArray());
                    break;
                case CombatantDefeated defeated:
                    if (!staged[defeated.CombatantId].IsDefeated)
                        throw new InvalidOperationException("Defeat proposal is inconsistent.");
                    break;
                default: throw new InvalidOperationException("Unsupported state change.");
            }
        }
        if (FailAfterStaging) throw new InvalidOperationException("Deliberate adapter failure after staging.");
        var nextVersion = checked(Version + 1);
        _state = staged;
        Version = nextVersion;
        _applied.Add(resolution.ResolutionId);
        Committed.Add(resolution);
        return CombatCommitStatus.Applied;
    }

    private static CombatantSnapshot Copy(CombatantSnapshot state, int health,
        IReadOnlyList<Domain.Combat.Effects.Models.CombatEffectSnapshot> effects) =>
        new(state.Id, health, state.MaxHealth, state.Armor, state.Evasion, state.Resistances, effects);
}
