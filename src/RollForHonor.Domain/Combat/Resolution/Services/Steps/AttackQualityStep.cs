using RollForHonor.Domain.Combat.Damage.Models;
using RollForHonor.Domain.Combat.Damage.Services;
using RollForHonor.Domain.Combat.Resolution.Models;
using RollForHonor.Domain.Dice.Services;

namespace RollForHonor.Domain.Combat.Resolution.Services.Steps;

/// <summary>Applies attack-outcome damage scaling once, after raw damage and before defenses.</summary>
public sealed class AttackQualityStep : ICombatStep
{
    private readonly IAttackQualityDamageResolver _resolver;

    /// <summary>Creates an optional attack-quality calculation step.</summary>
    /// <param name="resolver">The non-null outcome scaling policy.</param>
    /// <exception cref="ArgumentNullException">The resolver is null.</exception>
    public AttackQualityStep(IAttackQualityDamageResolver resolver) =>
        _resolver = resolver ?? throw new ArgumentNullException(nameof(resolver));

    /// <inheritdoc />
    /// <exception cref="InvalidOperationException">
    /// Raw damage or outcome is absent, scaling was already applied, or defenses have already run.
    /// </exception>
    public CombatContext Execute(CombatContext context, IDiceRoller dice)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(dice);
        var damage = context.BeforeDefenses
            ?? throw new InvalidOperationException("Attack quality requires raw damage.");
        var outcome = context.AttackOutcome
            ?? throw new InvalidOperationException("Attack quality requires an attack outcome.");
        if (context.AfterDefenses is not null)
            throw new InvalidOperationException("Attack quality must be applied before defenses.");
        if (damage.Breakdown.Steps.Any(step => step.Type == DamageStepType.AttackQuality))
            throw new InvalidOperationException("Attack quality has already been applied.");
        return context with { BeforeDefenses = _resolver.Resolve(damage, outcome) };
    }
}
