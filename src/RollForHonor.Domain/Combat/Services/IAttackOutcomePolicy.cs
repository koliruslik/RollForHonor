using RollForHonor.Domain.Combat.Attacks;
using RollForHonor.Domain.Combat.State;

namespace RollForHonor.Domain.Combat.Services;

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
