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
    /// Rolls each typed damage component, adds its flat value, clamps it to zero,
    /// then applies the outcome multiplier and rounds the component upward.
    /// Target defenses are not applied.
    /// </summary>
    /// <param name="attack">The attack containing the damage components.</param>
    /// <param name="source">The attacker snapshot reserved for attacker-dependent modifiers.</param>
    /// <param name="attackRoll">The previously resolved attack roll; it is not rolled again.</param>
    /// <param name="outcome">The outcome selecting the damage multiplier.</param>
    /// <param name="dice">The dice source that records performed damage rolls.</param>
    /// <returns>Unmitigated damage and its ordered calculation breakdown.</returns>
    /// <exception cref="ArgumentNullException">A reference argument is null.</exception>
    /// <exception cref="InvalidOperationException"><paramref name="outcome"/> is not defined.</exception>
    /// <exception cref="OverflowException">
    /// A component exceeds the integer range after addition or scaling.
    /// </exception>
    /// <remarks>
    /// Does not mutate combatant snapshots. Dice-source exceptions are propagated.
    /// </remarks>
    UnmitigatedDamage Resolve(
        AttackPayload attack,
        CombatantSnapshot source,
        AttackRollResult attackRoll,
        AttackOutcome outcome,
        IDiceRoller dice);
}
