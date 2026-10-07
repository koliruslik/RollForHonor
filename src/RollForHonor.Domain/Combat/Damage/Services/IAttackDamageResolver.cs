using RollForHonor.Domain.Combat.Attacks.Models;
using RollForHonor.Domain.Combat.Damage.Models;
using RollForHonor.Domain.Combat.State.Models;
using RollForHonor.Domain.Dice.Services;

namespace RollForHonor.Domain.Combat.Damage.Services;

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
