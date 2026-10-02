using RollForHonor.Domain.Combat.State;

namespace RollForHonor.Domain.Combat.StateChanges;

public sealed record CombatantDefeated : ICombatStateChange
{
    public CombatantId CombatantId { get; }

    public CombatantDefeated(CombatantId combatantId)
    {
        ArgumentNullException.ThrowIfNull(combatantId);
        CombatantId = combatantId;
    }
}
