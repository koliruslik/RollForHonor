using RollForHonor.Domain.Combat.Attacks;
using RollForHonor.Domain.Combat.Damage;
using RollForHonor.Domain.Combat.Effects;
using RollForHonor.Domain.Combat.State;

namespace RollForHonor.Domain.Combat.Services;

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
