using RollForHonor.Domain.Combat.StateChanges;
using RollForHonor.Domain.Combat.StateMutations;

namespace RollForHonor.Domain.Combat.Effects;

public sealed record EffectResolution
{
    public IReadOnlyList<ResolvedCombatEffect> Effects { get; }

    public IReadOnlyList<HealthAdjustment> HealthAdjustments { get; }

    public IReadOnlyList<ICombatStateChange> StateChanges { get; }

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
