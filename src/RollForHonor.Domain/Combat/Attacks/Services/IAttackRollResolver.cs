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
    /// Rolls the attack once and adds its flat attack modifier using checked arithmetic.
    /// </summary>
    /// <param name="attack">The attack whose roll formula and modifier are applied.</param>
    /// <param name="source">The attacking combatant available to source-dependent rules.</param>
    /// <param name="target">The target available to target-dependent rules.</param>
    /// <param name="dice">The dice source that records the performed attack roll.</param>
    /// <returns>The original dice roll, applied modifier, and final total.</returns>
    /// <exception cref="ArgumentNullException">A reference argument is null.</exception>
    /// <exception cref="OverflowException">The roll total and modifier exceed the integer range.</exception>
    AttackRollResult Resolve(
        AttackPayload attack,
        CombatantSnapshot source,
        CombatantSnapshot target,
        IDiceRoller dice);
}
