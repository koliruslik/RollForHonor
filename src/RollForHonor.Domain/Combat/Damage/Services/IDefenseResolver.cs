using RollForHonor.Domain.Combat.Damage.Models;
using RollForHonor.Domain.Combat.State.Models;

namespace RollForHonor.Domain.Combat.Damage.Services;

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
