using RollForHonor.Domain.Combat.Attacks;
using RollForHonor.Domain.Combat.Damage;
using RollForHonor.Domain.Combat.State;
using RollForHonor.Domain.Dice;

namespace RollForHonor.Domain.Combat.Services;

/// <summary>
/// Calculates attack damage before target-specific defenses.
/// </summary>
public interface IAttackDamageResolver
{
    /// <summary>
    /// Resolves unmitigated damage from the attack and its outcome.
    /// </summary>
    UnmitigatedDamage Resolve(
        AttackPayload attack,
        CombatantSnapshot source,
        AttackRollResult attackRoll,
        AttackOutcome outcome,
        IDiceRoller dice);
}
