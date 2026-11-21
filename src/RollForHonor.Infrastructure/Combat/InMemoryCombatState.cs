using System.Collections.Immutable;
using RollForHonor.Application.Combat.Models;
using RollForHonor.Application.Combat.Ports;
using RollForHonor.Application.Combat.Services;
using RollForHonor.Domain.Combat.Resolution.Models;
using RollForHonor.Domain.Combat.State.Changes;
using RollForHonor.Domain.Combat.State.Models;
using RollForHonor.Domain.Resources.Models;
using RollForHonor.Domain.Stats;

namespace RollForHonor.Infrastructure.Combat;

/// <summary>Owns versioned combat views, resources, cooldowns and receipts for one in-memory session.</summary>
/// <remarks>
/// Both ports share one conservative session version. Reads and publication are synchronized;
/// changes are prepared in immutable collections and published with one state replacement.
/// This adapter stores the currently supported combat views, not a full character/inventory model.
/// Nothing is persisted, and a new adapter starts a separate session.
/// </remarks>
public sealed class InMemoryCombatState : ICombatStateAdapter, IAttackInitiationAdapter
{
    private readonly object _gate = new();
    private SessionData _data = new();

    /// <summary>Registers one immutable combatant view and optional initial resource balances.</summary>
    /// <param name="combatant">A participant with a unique identity in this session.</param>
    /// <param name="resources">Optional unique, non-null resource balances; omitted resources have zero balance.</param>
    /// <exception cref="ArgumentNullException">The combatant is null.</exception>
    /// <exception cref="ArgumentException">The identity is already registered or resource entries are null/duplicated.</exception>
    /// <exception cref="OverflowException">The session version cannot be advanced.</exception>
    public void AddCombatant(CombatantSnapshot combatant, IReadOnlyList<ResourceAmount>? resources = null)
    {
        ArgumentNullException.ThrowIfNull(combatant);
        var amounts = resources?.ToArray() ?? [];
        if (amounts.Any(amount => amount is null))
            throw new ArgumentException("Resources cannot contain null.", nameof(resources));
        var pools = amounts.ToImmutableDictionary(amount => amount.Resource, amount => amount.Amount);
        lock (_gate)
        {
            _data = _data with
            {
                Version = checked(_data.Version + 1),
                Actors = _data.Actors.Add(combatant.Id, combatant),
                Resources = _data.Resources.Add(combatant.Id, pools)
            };
        }
    }

    /// <summary>Registers an actor from an immutable stat profile, initializing current HP and mana at their resolved maxima.</summary>
    /// <param name="id">The unique participant identity.</param>
    /// <param name="profile">Base values and external modifier entries.</param>
    /// <exception cref="ArgumentNullException">An input is null.</exception>
    /// <exception cref="ArgumentException">The identity is already registered.</exception>
    /// <exception cref="OverflowException">A derived combat value or session version cannot be represented.</exception>
    public void AddStatCombatant(CombatantId id, StatProfile profile)
    {
        ArgumentNullException.ThrowIfNull(id);
        ArgumentNullException.ThrowIfNull(profile);
        var stats = new StatCalculator().Calculate(profile);
        var projected = new CombatStatProjector().Project(new(id, 0, 1, 0, 0, [], []), stats);
        var actor = Copy(projected, projected.MaxHealth, projected.Effects);
        var mana = stats.Get(new(StatType.MaxMana));

        lock (_gate)
        {
            _data = _data with
            {
                Version = checked(_data.Version + 1),
                Actors = _data.Actors.Add(id, actor),
                Resources = _data.Resources.Add(id, ImmutableDictionary<ResourceId, decimal>.Empty.Add(new("mana"), mana)),
                Profiles = _data.Profiles.Add(id, profile),
                Stats = _data.Stats.Add(id, stats)
            };
        }
    }

    /// <summary>Reads immutable resolved attributes from the current session.</summary>
    /// <param name="id">A stat-managed participant identity.</param>
    /// <returns>The current resolved attribute set; current HP/mana are not part of it.</returns>
    /// <exception cref="KeyNotFoundException">The participant is not stat-managed.</exception>
    public StatBlock GetStats(CombatantId id)
    {
        lock (_gate)
        {
            return _data.Stats[id];
        }
    }

    /// <summary>Reads the immutable base definition and modifier inputs for a stat-managed actor.</summary>
    /// <param name="id">The participant identity.</param>
    /// <returns>The current input profile, independent of current health and resources.</returns>
    /// <exception cref="KeyNotFoundException">The participant is not stat-managed.</exception>
    public StatProfile GetStatProfile(CombatantId id)
    {
        lock (_gate)
        {
            return _data.Profiles[id];
        }
    }

    /// <summary>Atomically replaces stat inputs and their combat projection while retaining cooldowns, effects and receipts.</summary>
    /// <param name="id">A registered stat-managed participant.</param>
    /// <param name="profile">The complete new base definition and modifier list.</param>
    /// <remarks>Increasing maxima does not heal/refill; reducing them clamps current HP/mana downward. Existing snapshots remain unchanged.</remarks>
    /// <exception cref="ArgumentNullException">The profile is null.</exception>
    /// <exception cref="KeyNotFoundException">The participant is not stat-managed.</exception>
    /// <exception cref="OverflowException">Derived values or session version cannot be represented.</exception>
    public void UpdateStats(CombatantId id, StatProfile profile)
    {
        ArgumentNullException.ThrowIfNull(profile);
        var stats = new StatCalculator().Calculate(profile);

        lock (_gate)
        {
            if (!_data.Profiles.ContainsKey(id))
            {
                throw new KeyNotFoundException("The participant is not stat-managed.");
            }

            var actor = new CombatStatProjector().Project(_data.Actors[id], stats);
            var mana = new ResourceId("mana");
            var resources = _data.Resources[id].SetItem(mana,
                Math.Min(_data.Resources[id].GetValueOrDefault(mana), stats.Get(new(StatType.MaxMana))));
            _data = _data with
            {
                Version = checked(_data.Version + 1),
                Actors = _data.Actors.SetItem(id, actor),
                Resources = _data.Resources.SetItem(id, resources),
                Profiles = _data.Profiles.SetItem(id, profile),
                Stats = _data.Stats.SetItem(id, stats)
            };
        }
    }

    /// <summary>Replaces a registered combat view for explicit host-driven edits, retaining resources and launch receipts.</summary>
    /// <param name="combatant">The complete replacement view, including the effects the host intends to retain.</param>
    /// <exception cref="ArgumentNullException">The replacement is null.</exception>
    /// <exception cref="KeyNotFoundException">The participant is not registered.</exception>
    /// <exception cref="OverflowException">The session version cannot be advanced.</exception>
    /// <exception cref="InvalidOperationException">A direct edit conflicts with a stat-managed actor's derived defenses or maximum HP.</exception>
    /// <remarks>This is an explicit state edit, not the path for applying calculated combat damage. Stat-managed actors permit current-health/effect edits only; derived attributes must use UpdateStats, and their offensive projection is retained.</remarks>
    public void UpdateCombatant(CombatantSnapshot combatant)
    {
        ArgumentNullException.ThrowIfNull(combatant);
        lock (_gate)
        {
            if (!_data.Actors.TryGetValue(combatant.Id, out var current))
            {
                throw new KeyNotFoundException("The participant is not registered.");
            }

            if (_data.Profiles.ContainsKey(combatant.Id))
            {
                if (combatant.MaxHealth != current.MaxHealth || combatant.Armor != current.Armor
                    || combatant.Evasion != current.Evasion || !combatant.Resistances.SequenceEqual(current.Resistances))
                {
                    throw new InvalidOperationException("Use UpdateStats to edit derived attributes of a stat-managed actor.");
                }

                combatant = Copy(current, combatant.Health, combatant.Effects);
            }

            _data = _data with
            {
                Version = checked(_data.Version + 1),
                Actors = _data.Actors.SetItem(combatant.Id, combatant)
            };
        }
    }

    /// <inheritdoc />
    /// <exception cref="ArgumentNullException">A participant identity is null.</exception>
    /// <exception cref="KeyNotFoundException">A participant is not registered.</exception>
    public CombatStateRead Read(CombatantId sourceId, CombatantId targetId)
    {
        ArgumentNullException.ThrowIfNull(sourceId);
        ArgumentNullException.ThrowIfNull(targetId);
        lock (_gate) return new(_data.Version, _data.Actors[sourceId], _data.Actors[targetId]);
    }

    /// <inheritdoc />
    /// <exception cref="ArgumentNullException">Source identity is null.</exception>
    /// <exception cref="ArgumentException">The key/request identity is empty or a receipt belongs to another command.</exception>
    /// <exception cref="KeyNotFoundException">The source is not registered.</exception>
    public AttackInitiationState ReadForStart(CombatantId sourceId, string definitionKey, Guid requestId)
    {
        ArgumentNullException.ThrowIfNull(sourceId);
        ArgumentException.ThrowIfNullOrWhiteSpace(definitionKey);
        if (requestId == Guid.Empty) throw new ArgumentException("Request identity cannot be empty.", nameof(requestId));
        lock (_gate)
        {
            var receipt = FindReceipt(sourceId, definitionKey, requestId);
            var resources = _data.Resources[sourceId].OrderBy(pair => pair.Key.Value, StringComparer.Ordinal)
                .Select(pair => new ResourceAmount(pair.Key, pair.Value)).ToArray();
            return new(_data.Version, _data.Actors[sourceId], resources,
                _data.Cooldowns.GetValueOrDefault((sourceId, definitionKey)), receipt?.AttackId);
        }
    }

    /// <inheritdoc />
    /// <exception cref="ArgumentNullException">The proposal is null.</exception>
    /// <exception cref="ArgumentException">A receipt belongs to another command.</exception>
    /// <exception cref="KeyNotFoundException">The source is not registered.</exception>
    /// <exception cref="InvalidOperationException">Current balances cannot afford the proposal.</exception>
    /// <exception cref="OverflowException">The session version cannot be advanced.</exception>
    public CombatCommitStatus TryStart(AttackStartProposal proposal)
    {
        ArgumentNullException.ThrowIfNull(proposal);
        lock (_gate)
        {
            if (FindReceipt(proposal.SourceId, proposal.DefinitionKey, proposal.RequestId) is not null)
                return CombatCommitStatus.AlreadyApplied;
            if (proposal.ExpectedVersion != _data.Version) return CombatCommitStatus.VersionConflict;
            var pools = _data.Resources[proposal.SourceId].ToBuilder();
            foreach (var cost in proposal.Costs.Costs)
            {
                var balance = pools.GetValueOrDefault(cost.Resource);
                if (balance < cost.Amount) throw new InvalidOperationException("The launch proposal is unaffordable.");
                pools[cost.Resource] = balance - cost.Amount;
            }
            _data = _data with
            {
                Version = checked(_data.Version + 1),
                Resources = _data.Resources.SetItem(proposal.SourceId, pools.ToImmutable()),
                Cooldowns = _data.Cooldowns.SetItem((proposal.SourceId, proposal.DefinitionKey), proposal.ReadyAt),
                Starts = _data.Starts.Add(proposal.RequestId, new(proposal.SourceId, proposal.DefinitionKey, proposal.AttackId))
            };
            return CombatCommitStatus.Applied;
        }
    }

    /// <inheritdoc />
    /// <exception cref="ArgumentNullException">The resolution is null.</exception>
    /// <exception cref="KeyNotFoundException">A changed participant or the target is not registered.</exception>
    /// <exception cref="InvalidOperationException">Changes are unsupported, stale or inconsistent with registered state.</exception>
    /// <exception cref="ArgumentOutOfRangeException">A proposed health value is outside the participant's range.</exception>
    /// <exception cref="OverflowException">The session version cannot be advanced.</exception>
    public CombatCommitStatus TryCommit(CombatResolution resolution)
    {
        ArgumentNullException.ThrowIfNull(resolution);
        lock (_gate)
        {
            if (_data.Applied.Contains(resolution.ResolutionId)) return CombatCommitStatus.AlreadyApplied;
            if (resolution.BasedOnStateVersion != _data.Version) return CombatCommitStatus.VersionConflict;
            var actors = _data.Actors.ToBuilder();
            foreach (var change in resolution.Changes)
            {
                var id = change switch
                {
                    HealthChanged value => value.CombatantId,
                    EffectApplied value => value.CombatantId,
                    EffectRemoved value => value.CombatantId,
                    CombatantDefeated value => value.CombatantId,
                    _ => throw new InvalidOperationException("Unsupported combat state change.")
                };
                if (id != resolution.Result.SourceId && id != resolution.Result.Target.TargetId)
                    throw new InvalidOperationException("The change targets a participant outside the resolution.");
                var actor = actors[id];
                actors[id] = change switch
                {
                    HealthChanged health when actor.Health == health.PreviousHealth => Copy(actor, health.CurrentHealth, actor.Effects),
                    HealthChanged => throw new InvalidOperationException("The health proposal is stale."),
                    EffectApplied applied when !actor.Effects.Any(effect => effect.Id == applied.Effect.Id) =>
                        Copy(actor, actor.Health, actor.Effects.Append(applied.Effect).ToArray()),
                    EffectApplied => throw new InvalidOperationException("The effect identity is already active."),
                    EffectRemoved removed when actor.Effects.Any(effect => effect.Id == removed.EffectId) =>
                        Copy(actor, actor.Health, actor.Effects.Where(effect => effect.Id != removed.EffectId).ToArray()),
                    EffectRemoved => throw new InvalidOperationException("The removed effect is not active."),
                    CombatantDefeated when actor.IsDefeated => actor,
                    _ => throw new InvalidOperationException("The defeat proposal is inconsistent.")
                };
            }
            if (actors[resolution.Result.Target.TargetId].IsDefeated != resolution.Result.Target.IsDefeated)
                throw new InvalidOperationException("The result and projected target defeat disagree.");
            _data = _data with
            {
                Version = checked(_data.Version + 1),
                Actors = actors.ToImmutable(),
                Applied = _data.Applied.Add(resolution.ResolutionId)
            };
            return CombatCommitStatus.Applied;
        }
    }

    private StartReceipt? FindReceipt(CombatantId sourceId, string definitionKey, Guid requestId)
    {
        if (!_data.Starts.TryGetValue(requestId, out var receipt)) return null;
        if (receipt.SourceId != sourceId || receipt.Key != definitionKey)
            throw new ArgumentException("The request identity belongs to another launch command.", nameof(requestId));
        return receipt;
    }

    private static CombatantSnapshot Copy(CombatantSnapshot actor, int health,
        IReadOnlyList<Domain.Combat.Effects.Models.CombatEffectSnapshot> effects) =>
        new(actor.Id, health, actor.MaxHealth, actor.Armor, actor.Evasion, actor.Resistances, effects, actor.Offense);

    private sealed record StartReceipt(CombatantId SourceId, string Key, Guid AttackId);

    private sealed record SessionData
    {
        internal long Version { get; init; }
        internal ImmutableDictionary<CombatantId, StatProfile> Profiles { get; init; } = ImmutableDictionary<CombatantId, StatProfile>.Empty;
        internal ImmutableDictionary<CombatantId, StatBlock> Stats { get; init; } = ImmutableDictionary<CombatantId, StatBlock>.Empty;
        internal ImmutableDictionary<CombatantId, CombatantSnapshot> Actors { get; init; } = ImmutableDictionary<CombatantId, CombatantSnapshot>.Empty;
        internal ImmutableDictionary<CombatantId, ImmutableDictionary<ResourceId, decimal>> Resources { get; init; } = ImmutableDictionary<CombatantId, ImmutableDictionary<ResourceId, decimal>>.Empty;
        internal ImmutableDictionary<(CombatantId, string), TimeSpan> Cooldowns { get; init; } = ImmutableDictionary<(CombatantId, string), TimeSpan>.Empty;
        internal ImmutableDictionary<Guid, StartReceipt> Starts { get; init; } = ImmutableDictionary<Guid, StartReceipt>.Empty;
        internal ImmutableHashSet<Guid> Applied { get; init; } = ImmutableHashSet<Guid>.Empty;
    }
}
