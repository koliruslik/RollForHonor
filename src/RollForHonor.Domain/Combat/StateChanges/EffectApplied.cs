using RollForHonor.Domain.Combat.Effects;
using RollForHonor.Domain.Combat.State;

namespace RollForHonor.Domain.Combat.StateChanges;

public sealed record EffectApplied : ICombatStateChange
{
    public CombatantId CombatantId { get; }
    public CombatEffectSnapshot Effect { get; }

    public EffectApplied(
        CombatantId combatantId,
        CombatEffectSnapshot effect)
    {
        ArgumentNullException.ThrowIfNull(combatantId);
        ArgumentNullException.ThrowIfNull(effect);

        CombatantId = combatantId;
        Effect = effect;
    }
}
