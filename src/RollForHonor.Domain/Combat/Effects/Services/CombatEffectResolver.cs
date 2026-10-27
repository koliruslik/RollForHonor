using RollForHonor.Domain.Combat.Effects.Models;
using RollForHonor.Domain.Combat.Resolution.Models;
using RollForHonor.Domain.Combat.State.Changes;

namespace RollForHonor.Domain.Combat.Effects.Services;

/// <summary>
/// Proposes payload statuses for their declared recipients. A new definition replaces every
/// instance with the same definition ID on that recipient, independently of its source.
/// Never executes status behavior or changes the original snapshots.
/// </summary>
public sealed class CombatEffectResolver : ICombatEffectResolver
{
    private readonly IEffectInstanceIdFactory _ids;

    /// <summary>Creates a status resolver with an explicit identity provider.</summary>
    /// <param name="ids">Deterministic instance identity provider.</param>
    /// <exception cref="ArgumentNullException">The provider is null.</exception>
    public CombatEffectResolver(IEffectInstanceIdFactory ids) =>
        _ids = ids ?? throw new ArgumentNullException(nameof(ids));

    /// <inheritdoc />
    /// <exception cref="ArgumentNullException">The context is null.</exception>
    public EffectResolution Resolve(CombatContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        var request = context.Request;
        var snapshot = request.StateSnapshot;
        var participants = new[] { snapshot.Source, snapshot.Target }.DistinctBy(c => c.Id);
        var effective = participants.ToDictionary(c => c.Id, c => c.Effects.ToList());
        // Earlier proposals affect replacement decisions, but are never activated as behaviors.
        foreach (var change in context.StateChanges)
        {
            if (change is EffectRemoved removed && effective.TryGetValue(removed.CombatantId, out var removedList))
                removedList.RemoveAll(effect => effect.Id == removed.EffectId);
            else if (change is EffectApplied applied && effective.TryGetValue(applied.CombatantId, out var appliedList))
                appliedList.Add(applied.Effect);
        }
        var selections = new List<ResolvedCombatEffect>();
        var changes = new List<ICombatStateChange>();
        for (var index = 0; index < request.Attack.Effects.Count; index++)
        {
            var definition = request.Attack.Effects[index];
            var recipient = definition.Recipient == EffectRecipient.Source ? snapshot.Source : snapshot.Target;
            var instances = effective[recipient.Id];
            foreach (var old in instances.Where(effect => effect.Definition.Id == definition.Id).ToArray())
            {
                changes.Add(new EffectRemoved(recipient.Id, old.Id));
                instances.Remove(old);
            }
            var instance = new CombatEffectSnapshot(
                _ids.Create(request.ResolutionId, recipient.Id, definition.Id, index), definition);
            instances.Add(instance);
            changes.Add(new EffectApplied(recipient.Id, instance));
            selections.Add(new ResolvedCombatEffect(recipient.Id, definition));
        }
        return new EffectResolution(selections, [], changes);
    }
}
