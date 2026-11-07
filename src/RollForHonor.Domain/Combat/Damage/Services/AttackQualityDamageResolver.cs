using RollForHonor.Domain.Combat.Attacks.Models;
using RollForHonor.Domain.Combat.Damage.Configuration;
using RollForHonor.Domain.Combat.Damage.Models;

namespace RollForHonor.Domain.Combat.Damage.Services;

/// <summary>Applies configured outcome scaling without consuming dice or modifying the input.</summary>
public sealed class AttackQualityDamageResolver : IAttackQualityDamageResolver
{
    private readonly DamageMultipliers _multipliers;

    /// <summary>Creates an outcome scaling policy.</summary>
    /// <param name="multipliers">Non-negative multipliers for each attack outcome.</param>
    /// <exception cref="ArgumentNullException">Multipliers are null.</exception>
    public AttackQualityDamageResolver(DamageMultipliers multipliers) =>
        _multipliers = multipliers ?? throw new ArgumentNullException(nameof(multipliers));

    /// <inheritdoc />
    public DamageCalculationResult Resolve(DamageCalculationResult damage, AttackOutcome outcome)
    {
        ArgumentNullException.ThrowIfNull(damage);
        var multiplier = outcome switch
        {
            AttackOutcome.Evaded => _multipliers.Evaded,
            AttackOutcome.GlancingHit => _multipliers.GlancingHit,
            AttackOutcome.Hit => _multipliers.Hit,
            AttackOutcome.CriticalHit => _multipliers.CriticalHit,
            _ => throw new InvalidOperationException($"Unknown attack outcome {outcome}")
        };
        var scaled = new DamagePacket(damage.Damage.Components.Select(component =>
            new DamageAmount(component.Type, checked((int)Math.Ceiling(component.Amount * multiplier)))).ToArray());
        var breakdown = new DamageBreakdown(damage.Breakdown.Steps.Append(
            new DamageStep(DamageStepType.AttackQuality, damage.Damage, scaled)).ToArray());
        return new DamageCalculationResult(breakdown, scaled);
    }
}
