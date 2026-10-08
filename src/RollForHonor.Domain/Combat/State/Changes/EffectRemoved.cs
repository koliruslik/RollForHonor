using RollForHonor.Domain.Combat.Effects.Models;
using RollForHonor.Domain.Combat.State.Models;

namespace RollForHonor.Domain.Combat.State.Changes;

/// <summary>
/// Records that an effect instance must be removed from a combatant.
/// </summary>
public sealed record EffectRemoved : ICombatStateChange
{
    /// <summary>Gets the affected combatant identifier.</summary>
    public CombatantId CombatantId { get; }

    /// <summary>Gets the effect instance identifier to remove.</summary>
    public EffectInstanceId EffectId { get; }

    /// <summary>
    /// Creates an effect-removed state change.
    /// </summary>
    /// <param name="combatantId">The combatant losing the effect.</param>
    /// <param name="effectId">The runtime effect instance to remove.</param>
    /// <exception cref="ArgumentNullException">Either argument is null.</exception>
    public EffectRemoved(
        CombatantId combatantId,
        EffectInstanceId effectId)
    {
        ArgumentNullException.ThrowIfNull(combatantId);
        ArgumentNullException.ThrowIfNull(effectId);

        CombatantId = combatantId;
        EffectId = effectId;
    }
}
