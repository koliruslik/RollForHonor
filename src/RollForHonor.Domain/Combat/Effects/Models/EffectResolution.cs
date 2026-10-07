using RollForHonor.Domain.Combat.State.Changes;
using RollForHonor.Domain.Combat.State.Models;

namespace RollForHonor.Domain.Combat.Effects.Models;

/// <summary>
/// Groups resolved effects, health adjustments, and immediate state changes.
/// </summary>
public sealed record EffectResolution
{
    /// <summary>Gets effects reported in the combat result.</summary>
    public IReadOnlyList<ResolvedCombatEffect> Effects { get; }

    /// <summary>Gets proposed health adjustments produced by the effects.</summary>
    public IReadOnlyList<HealthAdjustment> HealthAdjustments { get; }

    /// <summary>Gets immediate non-health state changes produced by the effects.</summary>
    public IReadOnlyList<ICombatStateChange> StateChanges { get; }

    /// <summary>
    /// Creates an immutable effect-stage result.
    /// </summary>
    /// <param name="effects">Effects reported in the combat result.</param>
    /// <param name="healthAdjustments">Signed health adjustments for later projection.</param>
    /// <param name="stateChanges">Immediate non-health state changes.</param>
    /// <exception cref="ArgumentNullException">A collection argument is null.</exception>
    /// <exception cref="ArgumentException">
    /// A collection contains null, or state changes contain projected health or defeat changes.
    /// </exception>
    public EffectResolution(
        IReadOnlyList<ResolvedCombatEffect> effects,
        IReadOnlyList<HealthAdjustment> healthAdjustments,
        IReadOnlyList<ICombatStateChange> stateChanges)
    {
        ArgumentNullException.ThrowIfNull(effects);
        ArgumentNullException.ThrowIfNull(healthAdjustments);
        ArgumentNullException.ThrowIfNull(stateChanges);

        if (effects.Any(effect => effect is null))
        {
            throw new ArgumentException(
                "An effect resolution cannot contain null effects.",
                nameof(effects));
        }

        if (healthAdjustments.Any(adjustment => adjustment is null))
        {
            throw new ArgumentException(
                "An effect resolution cannot contain null health adjustments.",
                nameof(healthAdjustments));
        }

        if (stateChanges.Any(change => change is null))
        {
            throw new ArgumentException(
                "An effect resolution cannot contain null state changes.",
                nameof(stateChanges));
        }

        if (stateChanges.Any(change => change is HealthChanged or CombatantDefeated))
        {
            throw new ArgumentException(
                "Health changes and defeat must be produced by the health state projector.",
                nameof(stateChanges));
        }

        Effects = effects.ToArray();
        HealthAdjustments = healthAdjustments.ToArray();
        StateChanges = stateChanges.ToArray();
    }
}
