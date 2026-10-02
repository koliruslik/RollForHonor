using RollForHonor.Domain.Combat.Attacks;
using RollForHonor.Domain.Combat.Damage;
using RollForHonor.Domain.Combat.Effects;
using RollForHonor.Domain.Combat.State;

namespace RollForHonor.Domain.Combat.Services;

public interface ICombatEffectResolver
{
    EffectResolution Resolve(
        AttackPayload attack,
        CombatantSnapshot source,
        CombatantSnapshot target,
        AttackRollResult attackRoll,
        AttackOutcome outcome,
        UnmitigatedDamage damage,
        FinalDamage finalDamage);
}
