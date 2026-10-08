using RollForHonor.Domain.Combat.Attacks.Models;
using RollForHonor.Domain.Combat.State.Models;

namespace RollForHonor.Domain.Combat.Attacks.Services;

/// <summary>
/// Classifies an attack roll according to combat outcome rules.
/// </summary>
public interface IAttackOutcomePolicy
{
    /// <summary>
    /// Classifies the roll by its margin against target evasion, then applies
    /// the attack's natural-roll upgrade or downgrade rule.
    /// </summary>
    /// <param name="attack">The attack containing the roll and natural-shift rules.</param>
    /// <param name="source">The attacking combatant available to outcome rules.</param>
    /// <param name="target">The target whose evasion defines the attack margin.</param>
    /// <param name="attackRoll">The previously resolved attack roll.</param>
    /// <returns>The final attack outcome after margin classification and natural shifting.</returns>
    /// <exception cref="ArgumentNullException">A reference argument is null.</exception>
    /// <exception cref="ArgumentException">
    /// The attack does not use 1d20, or the supplied roll uses another formula.
    /// </exception>
    AttackOutcome Determine(
        AttackPayload attack,
        CombatantSnapshot source,
        CombatantSnapshot target,
        AttackRollResult attackRoll);
}
