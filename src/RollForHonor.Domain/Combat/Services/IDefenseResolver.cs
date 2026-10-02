using RollForHonor.Domain.Combat.Damage;
using RollForHonor.Domain.Combat.State;

namespace RollForHonor.Domain.Combat.Services;

public interface IDefenseResolver
{
    FinalDamage Resolve(UnmitigatedDamage unmitigatedDamage, CombatantSnapshot target);
}