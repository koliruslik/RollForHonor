using System.Collections.Immutable;
using RollForHonor.Domain.Combat.Damage.Models;
using RollForHonor.Domain.Combat.Resolution.Models;
using RollForHonor.Domain.Combat.State.Changes;
using RollForHonor.Domain.Combat.State.Models;
using RollForHonor.Domain.Combat.State.Services;
using RollForHonor.Domain.Dice.Models;

namespace RollForHonor.Domain.Combat.Resolution.Services;

/// <summary>
/// Performs mandatory finalization after all configurable calculation steps:
/// collects health proposals, projects participants, builds changes and constructs the result.
/// </summary>
/// <remarks>
/// Not a configurable pipeline step. Every action uses the same finalization order.
/// Inputs and live state are never mutated; rebuilding from the same context does not
/// accumulate changes. Damage and healing are combined before one clamped projection per participant.
/// </remarks>
public sealed class CombatResolutionBuilder
{
    private readonly IHealthStateProjector _projector;

    /// <summary>Creates a result builder with explicit health projection rules.</summary>
    /// <param name="projector">The non-null deterministic health projector.</param>
    /// <exception cref="ArgumentNullException">The projector is null.</exception>
    public CombatResolutionBuilder(IHealthStateProjector projector) =>
        _projector = projector ?? throw new ArgumentNullException(nameof(projector));

    /// <summary>Completes a calculation without running more effects or consuming dice.</summary>
    /// <param name="context">The completed calculation's inputs and proposals.</param>
    /// <param name="diceTrace">The action's completed dice trace.</param>
    /// <returns>A versioned result, canonical proposed changes and the complete execution trace.</returns>
    /// <exception cref="ArgumentNullException">An argument is null.</exception>
    /// <exception cref="InvalidOperationException">
    /// Post-defense damage has no original result, a proposal targets an unknown participant,
    /// a calculation attempts to prebuild health/defeat changes, or a state change is unsupported.
    /// </exception>
    /// <exception cref="OverflowException">Combined direct damage exceeds the supported integer range.</exception>
    public CombatResolution Build(CombatContext context, DiceTrace diceTrace)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(diceTrace);
        var adjustments = CollectHealthAdjustments(context);
        var projections = ProjectHealth(context.Request.StateSnapshot, adjustments);
        var changes = BuildStateChanges(context, projections);
        var result = BuildCombatResult(context, projections);
        var diceCount = diceTrace.Rolls.Count;
        var trace = new CombatStepTrace(nameof(CombatResolutionBuilder), diceCount, diceCount,
        [
            new(nameof(CollectHealthAdjustments), diceCount, diceCount, []),
            new(nameof(ProjectHealth), diceCount, diceCount, []),
            new(nameof(BuildStateChanges), diceCount, diceCount, []),
            new(nameof(BuildCombatResult), diceCount, diceCount, [])
        ]);
        return new CombatResolution(context.Request.ResolutionId, context.Request.StateSnapshot.Version,
            result, changes, diceTrace, context.StepTrace.Add(trace));
    }

    private static ImmutableArray<HealthAdjustment> CollectHealthAdjustments(CombatContext context)
    {
        if (context.AfterDefenses is not null && context.BeforeDefenses is null)
            throw new InvalidOperationException("Damage after defenses requires an original damage result.");
        var damage = context.AfterDefenses ?? context.BeforeDefenses;
        var amount = damage?.Sum() ?? 0;
        return amount > 0
            ? context.HealthAdjustments.Add(new HealthAdjustment(context.Request.TargetId, -amount))
            : context.HealthAdjustments;
    }

    private ImmutableArray<HealthProjection> ProjectHealth(CombatStateSnapshot snapshot,
        IReadOnlyList<HealthAdjustment> adjustments)
    {
        var participants = snapshot.Source.Id == snapshot.Target.Id
            ? new[] { snapshot.Source } : new[] { snapshot.Source, snapshot.Target };
        var ids = participants.Select(participant => participant.Id).ToHashSet();
        if (adjustments.Any(adjustment => !ids.Contains(adjustment.CombatantId)))
            throw new InvalidOperationException("A health adjustment targets a combatant outside the snapshot.");
        return participants.Select(participant => _projector.Project(participant,
            adjustments.Where(adjustment => adjustment.CombatantId == participant.Id).ToArray())).ToImmutableArray();
    }

    private static ImmutableArray<ICombatStateChange> BuildStateChanges(CombatContext context,
        IReadOnlyList<HealthProjection> projections)
    {
        var snapshot = context.Request.StateSnapshot;
        foreach (var change in context.StateChanges)
        {
            var id = change switch
            {
                EffectApplied applied => applied.CombatantId,
                EffectRemoved removed => removed.CombatantId,
                HealthChanged or CombatantDefeated => throw new InvalidOperationException(
                    "Only result finalization may propose health and defeat changes."),
                _ => throw new InvalidOperationException("Unsupported combat state change.")
            };
            if (id != snapshot.Source.Id && id != snapshot.Target.Id)
                throw new InvalidOperationException("A state change targets a combatant outside the snapshot.");
        }
        var changes = ImmutableArray.CreateBuilder<ICombatStateChange>();
        changes.AddRange(projections.Where(p => p.HasChanged)
            .Select(p => new HealthChanged(p.CombatantId, p.PreviousHealth, p.CurrentHealth)));
        changes.AddRange(context.StateChanges);
        changes.AddRange(projections.Where(p => p.BecameDefeated)
            .Select(p => new CombatantDefeated(p.CombatantId)));
        return changes.ToImmutable();
    }

    private static CombatResult BuildCombatResult(CombatContext context, IReadOnlyList<HealthProjection> projections)
    {
        var targetHealth = projections.Single(p => p.CombatantId == context.Request.TargetId);
        var damage = context.BeforeDefenses is null ? null : new DamageResolution(
            context.BeforeDefenses, context.AfterDefenses ?? context.BeforeDefenses);
        var target = new TargetCombatResult(context.Request.TargetId, context.AttackRoll,
            context.AttackOutcome, damage, context.EffectsApplied, targetHealth.CurrentHealth == 0);
        return new CombatResult(context.Request.SourceId, target);
    }
}
