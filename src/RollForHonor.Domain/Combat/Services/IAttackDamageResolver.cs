using RollForHonor.Domain.Combat.Attacks;
using RollForHonor.Domain.Combat.Damage;
using RollForHonor.Domain.Combat.State;
using RollForHonor.Domain.Dice;

namespace RollForHonor.Domain.Combat.Services;

public interface IAttackDamageResolver
{
    UnmitigatedDamage Resolve(
        AttackPayload attack,
        CombatantSnapshot source,
        AttackRollResult attackRoll,
        AttackOutcome outcome,
        IDiceRoller dice);
}
