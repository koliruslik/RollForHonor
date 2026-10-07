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
    /// Resolves effects after attack outcome and final damage are known.
    /// </summary>
    EffectResolution Resolve(
        AttackPayload attack,
        CombatantSnapshot source,
        CombatantSnapshot target,
        AttackRollResult attackRoll,
        AttackOutcome outcome,
        UnmitigatedDamage damage,
        FinalDamage finalDamage);
}
