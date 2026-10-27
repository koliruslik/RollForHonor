using RollForHonor.Domain.Combat.Effects.Models;
using RollForHonor.Domain.Combat.State.Models;

namespace RollForHonor.Domain.Combat.Effects.Services;

/// <summary>Supplies deterministic effect identities without global random state.</summary>
public interface IEffectInstanceIdFactory
{
    /// <summary>Creates the identity for one occurrence in an action's effect payload.</summary>
    /// <param name="resolutionId">Identity of the action being calculated.</param>
    /// <param name="recipientId">Resolved status recipient.</param>
    /// <param name="definitionId">Stable status definition.</param>
    /// <param name="occurrence">Zero-based index in the attack's effect payload.</param>
    /// <returns>The same non-empty identity for the same inputs.</returns>
    EffectInstanceId Create(Guid resolutionId, CombatantId recipientId,
        EffectDefinitionId definitionId, int occurrence);
}
