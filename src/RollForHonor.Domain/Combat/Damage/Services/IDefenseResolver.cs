using RollForHonor.Domain.Combat.Damage.Models;
using RollForHonor.Domain.Combat.State.Models;

namespace RollForHonor.Domain.Combat.Damage.Services;

/// <summary>
/// Applies target defenses to unmitigated damage.
/// </summary>
public interface IDefenseResolver
{
    /// <summary>
    /// Applies target-specific defenses while preserving typed damage and its breakdown.
    /// </summary>
    /// <param name="unmitigatedDamage">Damage produced before target defenses.</param>
    /// <param name="target">The target whose defenses are applied.</param>
    /// <returns>The typed damage remaining after mitigation.</returns>
    FinalDamage Resolve(
        UnmitigatedDamage unmitigatedDamage,
        CombatantSnapshot target);
}
