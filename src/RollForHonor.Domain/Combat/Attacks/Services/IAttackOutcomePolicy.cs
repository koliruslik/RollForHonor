using RollForHonor.Domain.Combat.Attacks.Models;
using RollForHonor.Domain.Combat.State.Models;

namespace RollForHonor.Domain.Combat.Attacks.Services;

/// <summary>
/// Classifies an attack roll according to combat outcome rules.
/// </summary>
public interface IAttackOutcomePolicy
{
    /// <summary>
    /// Determines the outcome category of an attack roll.
    /// </summary>
    AttackOutcome Determine(
        AttackPayload attack,
        CombatantSnapshot source,
        CombatantSnapshot target,
        AttackRollResult attackRoll);
}
