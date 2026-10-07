using RollForHonor.Domain.Combat.Attacks.Models;
using RollForHonor.Domain.Combat.Damage.Models;
using RollForHonor.Domain.Combat.Effects.Models;
using RollForHonor.Domain.Combat.State.Models;

namespace RollForHonor.Domain.Combat.Effects.Services;

/// <summary>
/// Resolves effects and their immediate mutations for one attack.
/// </summary>
public interface ICombatEffectResolver
{
    /// <summary>
    /// Resolves immediate attack effects after the outcome and both damage stages are known.
    /// </summary>
    /// <param name="attack">The attack carrying the effect definitions.</param>
    /// <param name="source">The combatant applying the effects.</param>
    /// <param name="target">The combatant receiving the effects.</param>
    /// <param name="attackRoll">The previously resolved attack roll.</param>
    /// <param name="outcome">The classified attack outcome.</param>
    /// <param name="damage">Damage before target defenses.</param>
    /// <param name="finalDamage">Damage remaining after target defenses.</param>
    /// <returns>Resolved effects, health adjustments, and immediate state changes.</returns>
    EffectResolution Resolve(
        AttackPayload attack,
        CombatantSnapshot source,
        CombatantSnapshot target,
        AttackRollResult attackRoll,
        AttackOutcome outcome,
        UnmitigatedDamage damage,
        FinalDamage finalDamage);
}
