using RollForHonor.Domain.Combat.Attacks.Models;
using RollForHonor.Domain.Combat.Damage.Models;

namespace RollForHonor.Domain.Combat.Damage.Services;

/// <summary>Applies an attack outcome's multiplier independently of rolling damage.</summary>
public interface IAttackQualityDamageResolver
{
    /// <summary>Scales each component, rounding upward, and appends the attack-quality breakdown.</summary>
    /// <param name="damage">Damage and its existing calculation history.</param>
    /// <param name="outcome">The classified outcome selecting the configured multiplier.</param>
    /// <returns>A new calculation result preserving component types, order, and previous history.</returns>
    /// <exception cref="ArgumentNullException">Damage is null.</exception>
    /// <exception cref="InvalidOperationException">The outcome is unknown.</exception>
    /// <exception cref="OverflowException">A scaled component exceeds the supported numeric range.</exception>
    DamageCalculationResult Resolve(DamageCalculationResult damage, AttackOutcome outcome);
}
