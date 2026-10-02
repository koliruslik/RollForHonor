using RollForHonor.Domain.Combat.Attacks;
using RollForHonor.Domain.Combat.State;
using RollForHonor.Domain.Dice;

namespace RollForHonor.Domain.Combat.Services;

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
