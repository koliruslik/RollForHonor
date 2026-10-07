using RollForHonor.Domain.Combat.Attacks.Models;
using RollForHonor.Domain.Combat.State.Models;
using RollForHonor.Domain.Dice.Services;

namespace RollForHonor.Domain.Combat.Attacks.Services;

/// <summary>
/// Resolves the dice result used to evaluate an attack.
/// </summary>
public interface IAttackRollResolver
{
    /// <summary>
    /// Rolls and modifies an attack for the supplied source and target.
    /// </summary>
    AttackRollResult Resolve(
        AttackPayload attack,
        CombatantSnapshot source,
        CombatantSnapshot target,
        IDiceRoller dice);
}
