using RollForHonor.Domain.Combat.Effects.Services;
using RollForHonor.Domain.Combat.Resolution.Models;
using RollForHonor.Domain.Dice.Services;

namespace RollForHonor.Domain.Combat.Resolution.Services.Steps;

/// <summary>Proposes new statuses independently of executing existing effect behaviors.</summary>
public sealed class ApplyNewEffectsStep : ICombatStep
{
    private readonly ICombatEffectResolver _resolver;

    /// <summary>Creates a status application step.</summary>
    /// <param name="resolver">The non-null status selection and replacement policy.</param>
    /// <exception cref="ArgumentNullException">The resolver is null.</exception>
    public ApplyNewEffectsStep(ICombatEffectResolver resolver) =>
        _resolver = resolver ?? throw new ArgumentNullException(nameof(resolver));

    /// <inheritdoc />
    public CombatContext Execute(CombatContext context, IDiceRoller dice)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(dice);
        if (context.NewEffectsResolved)
            throw new InvalidOperationException("New effects have already been resolved.");
        var result = _resolver.Resolve(context);
        if (result.HealthAdjustments.Count != 0)
            throw new InvalidOperationException("Status application must not execute health-changing behaviors.");
        return context with
        {
            EffectsApplied = context.EffectsApplied.AddRange(result.Effects),
            StateChanges = context.StateChanges.AddRange(result.StateChanges),
            NewEffectsResolved = true
        };
    }
}
