using RollForHonor.Domain.Combat.Damage;
using RollForHonor.Domain.Combat.State;

namespace RollForHonor.Domain.Combat.Services;

/// <summary>
/// Applies target defenses to unmitigated damage.
/// </summary>
public interface IDefenseResolver
{
    /// <summary>
    /// Resolves the typed damage that remains after target mitigation.
    /// </summary>
    FinalDamage Resolve(
        UnmitigatedDamage unmitigatedDamage,
        CombatantSnapshot target);
}
