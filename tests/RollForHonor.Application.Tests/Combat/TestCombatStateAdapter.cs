using RollForHonor.Application.Combat.Models;
using RollForHonor.Application.Combat.Ports;
using RollForHonor.Domain.Combat.Resolution.Models;
using RollForHonor.Domain.Combat.State.Changes;
using RollForHonor.Domain.Combat.State.Models;

namespace RollForHonor.Application.Tests.Combat;

// Test adapter only: does not select the game's runtime storage or concurrency strategy.
internal sealed class TestCombatStateAdapter(params CombatantSnapshot[] characters) : ICombatStateAdapter
{
    private Dictionary<CombatantId, CombatantSnapshot> _state = characters.ToDictionary(value => value.Id);
    private readonly HashSet<Guid> _applied = [];
    public long Version { get; private set; }
    public int CommitAttempts { get; private set; }
    public List<CombatStateRead> Reads { get; } = [];
    public List<CombatResolution> Committed { get; } = [];
    public Action<CombatResolution>? BeforeCommit { get; set; }
    public bool FailAfterStaging { get; set; }

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
