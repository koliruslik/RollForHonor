using RollForHonor.Application.Combat.Models;
using RollForHonor.Application.Combat.Ports;
using RollForHonor.Domain.Combat.Attacks.Services;
using RollForHonor.Domain.Combat.Damage.Configuration;
using RollForHonor.Domain.Combat.Damage.Services;
using RollForHonor.Domain.Combat.Effects.Services;
using RollForHonor.Domain.Combat.Resolution.Services;
using RollForHonor.Domain.Combat.Resolution.Services.Steps;
using RollForHonor.Domain.Combat.State.Models;

namespace RollForHonor.Application.Combat.Services;

/// <summary>Composes the existing basic attack rules and new-status application without selecting extra effect behaviors.</summary>
/// <remarks>Supports ordinary attack payloads; periodic tick scheduling and stat/equipment aggregation are separate consumers.</remarks>
public sealed class BasicAttackPipelineFactory : ICombatPipelineFactory
{
    private readonly CombatPipeline _pipeline;

    /// <summary>Creates reusable stateless calculation steps with explicit hit-quality coefficients.</summary>
    /// <param name="multipliers">Nonnegative quality coefficients; the composition root chooses the balance values.</param>
    /// <exception cref="ArgumentNullException">The coefficients are null.</exception>
    public BasicAttackPipelineFactory(DamageMultipliers multipliers)
    {
        ArgumentNullException.ThrowIfNull(multipliers);
        _pipeline = new([
            new AttackRollStep(new AttackRollResolver()), new AttackOutcomeStep(new D20AttackOutcomePolicy()),
            new RawDamageStep(new RawDamageResolver()), new AttackQualityStep(new AttackQualityDamageResolver(multipliers)),
            new DefenseStep(new DefenseResolver(new HitMagnitudeArmorFormula())),
            new ApplyNewEffectsStep(new CombatEffectResolver(new ResolutionEffectInstanceIdFactory()))
        ]);
    }

    /// <inheritdoc />
    /// <exception cref="ArgumentNullException">Attack or snapshot is null.</exception>
    public ICombatStep Create(AttackInstance attack, CombatStateSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(attack);
        ArgumentNullException.ThrowIfNull(snapshot);
        return _pipeline;
    }
}
