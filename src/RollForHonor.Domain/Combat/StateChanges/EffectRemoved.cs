using RollForHonor.Domain.Combat.Effects;
using RollForHonor.Domain.Combat.State;

namespace RollForHonor.Domain.Combat.StateChanges;

public sealed record EffectRemoved : ICombatStateChange
{
    public CombatantId CombatantId { get; }
    public EffectInstanceId EffectId { get; }

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
