using RollForHonor.Application.Combat.Models;
using RollForHonor.Application.Combat.Ports;
using RollForHonor.Domain.Combat.Attacks.Services;
using RollForHonor.Domain.Combat.Damage.Configuration;
using RollForHonor.Domain.Combat.Damage.Services;
using RollForHonor.Domain.Combat.Effects.Services;
using RollForHonor.Domain.Combat.Effects.Models;
using RollForHonor.Domain.Combat.Resolution.Services;
using RollForHonor.Domain.Combat.Resolution.Services.Steps;
using RollForHonor.Domain.Combat.State.Models;
using RollForHonor.Domain.Combat.State.Services;

namespace RollForHonor.Application.Combat.Services;

/// <summary>Composes basic attack rules, new-status application and explicitly selected supported attack behaviors.</summary>
/// <remarks>Supports ordinary attack payloads; periodic tick scheduling and stat/equipment aggregation are separate consumers.</remarks>
public sealed class BasicAttackPipelineFactory : ICombatPipelineFactory
{
    private readonly CombatPipeline _pipeline;
    private readonly OnHitStatusRule[] _statusRules;

    /// <summary>Creates reusable stateless calculation steps with explicit hit-quality coefficients.</summary>
    /// <param name="multipliers">Nonnegative quality coefficients; the composition root chooses the balance values.</param>
    /// <param name="ratingRules">Whether to use rating armor, natural-one misses and independent criticals.</param>
    /// <param name="statusRules">Optional explicit production status content; omitted disables on-hit status generation.</param>
    /// <exception cref="ArgumentNullException">The coefficients are null.</exception>
    public BasicAttackPipelineFactory(DamageMultipliers multipliers, bool ratingRules = false, IEnumerable<OnHitStatusRule>? statusRules = null)
    {
        ArgumentNullException.ThrowIfNull(multipliers);
        _statusRules = statusRules?.ToArray() ?? [];
        _ = new ApplyOnHitStatusesStep(_statusRules, null);
        var steps = new List<ICombatStep>
        {
            new AttackRollStep(new AttackRollResolver()),
            new AttackOutcomeStep(ratingRules ? new RatingAttackOutcomePolicy() : new D20AttackOutcomePolicy()),
            new RawDamageStep(new RawDamageResolver()), new SourceDamageStep(),
            new AttackQualityStep(new AttackQualityDamageResolver(multipliers))
        };

        if (ratingRules)
        {
            steps.Add(new IndependentCriticalStep());
        }

        steps.Add(new DefenseStep(new DefenseResolver(ratingRules ? new RatingArmorFormula() : new HitMagnitudeArmorFormula())));
        steps.Add(new ApplyNewEffectsStep(new CombatEffectResolver(new ResolutionEffectInstanceIdFactory())));
        _pipeline = new(steps);

    }

    /// <inheritdoc />
    /// <exception cref="ArgumentNullException">Attack or snapshot is null.</exception>
    /// <exception cref="InvalidOperationException">A behavior is unsupported or lifesteal is specified more than once.</exception>
    public ICombatStep Create(AttackInstance attack, CombatStateSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(attack);
        ArgumentNullException.ThrowIfNull(snapshot);
        var pipeline = _statusRules.Length == 0 ? _pipeline : new CombatPipeline(
            [_pipeline, new ApplyOnHitStatusesStep(_statusRules, attack.AttacksPerSecond)]);
        var behaviors = attack.Attack.Behaviors;
        if (behaviors.Count == 0) return pipeline;
        if (behaviors.Count != 1 || behaviors[0] is not LifestealDefinition lifesteal)
            throw new InvalidOperationException("The basic factory supports one lifesteal behavior; stacking/unknown behaviors require explicit composition.");
        return new CombatPipeline([pipeline,
            new ApplyEffectsStep([new LifestealEffect(lifesteal, new HealthStateProjector())], "AfterTargetHealthEffects")]);
    }
}
