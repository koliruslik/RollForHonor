using RollForHonor.Application.Combat.Models;
using RollForHonor.Application.Combat.Diagnostics;
using RollForHonor.Application.Combat.Ports;
using RollForHonor.Domain.Combat.State.Models;
using RollForHonor.Domain.Resources.Services;

namespace RollForHonor.Application.Combat.Services;

/// <summary>Accepts an attack once, calculating launch costs in Domain and committing them with cooldown.</summary>
public sealed class AttackInitiationService
{
    private readonly IAttackInitiationAdapter _state;
    private readonly IResourceCostResolver _costs;
    private readonly IResourceCostModifierProvider _modifiers;
    private readonly CombatImpactService _impacts;
    private readonly Func<TimeSpan> _now;
    private readonly ICombatDiagnostics? _diagnostics;

    /// <summary>Creates an engine-independent launch coordinator.</summary>
    /// <param name="state">Atomic initiation reads and commits.</param>
    /// <param name="costs">The deterministic Domain cost calculation.</param>
    /// <param name="modifiers">Application-owned selection of current source cost modifiers.</param>
    /// <param name="impacts">The existing per-impact use case.</param>
    /// <param name="now">The monotonic game timeline; pause/time scaling belong to the host.</param>
    /// <param name="diagnostics">Optional diagnostic output, isolated from launch success.</param>
    /// <remarks>Diagnostics are optional and their failures are isolated from launch.</remarks>
    public AttackInitiationService(IAttackInitiationAdapter state, IResourceCostResolver costs,
        IResourceCostModifierProvider modifiers, CombatImpactService impacts, Func<TimeSpan> now, ICombatDiagnostics? diagnostics = null)
    {
        _state = state ?? throw new ArgumentNullException(nameof(state));
        _costs = costs ?? throw new ArgumentNullException(nameof(costs));
        _modifiers = modifiers ?? throw new ArgumentNullException(nameof(modifiers));
        _impacts = impacts ?? throw new ArgumentNullException(nameof(impacts));
        _now = now ?? throw new ArgumentNullException(nameof(now));
        _diagnostics = diagnostics;
    }

    /// <summary>Attempts one accepted launch without calculating a hit or creating an engine projectile.</summary>
    /// <param name="requestId">A nonempty idempotency identity; retries of the same command retain it.</param>
    /// <param name="sourceId">The source paying the launch cost.</param>
    /// <param name="definition">Base attack, costs and cooldown.</param>
    /// <returns>A new attack only on a newly committed start; replay does not spawn another attack.</returns>
    /// <remarks>Rejected/stale attempts have no side effects. Exceptions propagate. No automatic retry occurs.</remarks>
    public AttackStartResult Start(Guid requestId, CombatantId sourceId, AttackDefinition definition)
    {
        if (requestId == Guid.Empty) throw new ArgumentException("A launch request identity cannot be empty.", nameof(requestId));
        ArgumentNullException.ThrowIfNull(sourceId);
        ArgumentNullException.ThrowIfNull(definition);
        return AttackStartDiagnostics.Run(_diagnostics, requestId, sourceId, definition,
            diagnostics => StartCore(requestId, sourceId, definition, diagnostics));
    }

    private AttackStartResult StartCore(Guid requestId, CombatantId sourceId, AttackDefinition definition,
        AttackStartDiagnostics diagnostics)
    {
        var state = _state.ReadForStart(sourceId, definition.Key, requestId)
            ?? throw new InvalidOperationException("The initiation adapter returned null.");
        diagnostics.ObserveState(state);
        if (state.Source.Id != sourceId)
            throw new InvalidOperationException("The initiation read does not match the requested source.");
        if (state.StartedAttackId is { } previous)
            return new AttackStartResult(AttackStartStatus.AlreadyStarted, attackId: previous);
        if (state.Source.IsDefeated) return new AttackStartResult(AttackStartStatus.SourceDefeated);
        var now = _now();
        diagnostics.ObserveTime(now);
        if (now < state.ReadyAt) return new AttackStartResult(AttackStartStatus.OnCooldown);

        var modifiers = _modifiers.GetReductions(state.Source)
            ?? throw new InvalidOperationException("The cost modifier provider returned null.");
        var costs = _costs.Resolve(definition.BaseCosts, modifiers)
            ?? throw new InvalidOperationException("The resource cost resolver returned null.");
        var balances = state.Resources.ToDictionary(value => value.Resource, value => value.Amount);
        if (costs.Costs.Any(cost => balances.GetValueOrDefault(cost.Resource) < cost.Amount))
            return new AttackStartResult(AttackStartStatus.InsufficientResources, costs: costs);

        var readyAt = now + definition.Cooldown;
        var attack = new AttackInstance(sourceId, definition.Attack, _impacts, definition.ExecutionRules, _now, now, definition.Cooldown);
        var proposal = new AttackStartProposal(requestId, attack.Id, sourceId, definition.Key, state.Version, costs, readyAt);
        diagnostics.ObserveProposal(proposal);
        return _state.TryStart(proposal) switch
        {
            CombatCommitStatus.Applied => new AttackStartResult(AttackStartStatus.Started, attack, costs: costs),
            CombatCommitStatus.VersionConflict => new AttackStartResult(AttackStartStatus.VersionConflict, costs: costs),
            CombatCommitStatus.AlreadyApplied => new AttackStartResult(AttackStartStatus.AlreadyStarted, costs: costs),
            _ => throw new InvalidOperationException("The adapter returned an unknown initiation status.")
        };
    }
}
