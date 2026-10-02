using RollForHonor.Domain.Combat.State;
using RollForHonor.Domain.Combat.StateMutations;

namespace RollForHonor.Domain.Combat.Services;

public interface IHealthStateProjector
{
    HealthProjection Project(
        CombatantSnapshot combatant,
        IReadOnlyList<HealthAdjustment> adjustments);
}
