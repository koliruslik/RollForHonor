using RollForHonor.Domain.Combat.Attacks;
using RollForHonor.Domain.Combat.State;

namespace RollForHonor.Domain.Combat.Services;

public interface IAttackOutcomePolicy
{
    AttackOutcome Determine(
        AttackPayload attack,
        CombatantSnapshot source,
        CombatantSnapshot target,
        AttackRollResult attackRoll);
}
