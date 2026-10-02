using RollForHonor.Domain.Combat.Effects;
using RollForHonor.Domain.Combat.State;

namespace RollForHonor.Domain.Combat.StateChanges;

/// <summary>
/// Records that an effect instance must be added to a combatant.
/// </summary>
public sealed record EffectApplied : ICombatStateChange
{
    /// <summary>Gets the affected combatant identifier.</summary>
    public CombatantId CombatantId { get; }

    /// <summary>Gets the effect instance to add.</summary>
    public CombatEffectSnapshot Effect { get; }

    /// <summary>
    /// Creates an effect-applied state change.
    /// </summary>
    public EffectApplied(
        CombatantId combatantId,
        CombatEffectSnapshot effect)
    {
        ArgumentNullException.ThrowIfNull(combatantId);
        ArgumentNullException.ThrowIfNull(effect);

        CombatantId = combatantId;
        Effect = effect;
    }
}
