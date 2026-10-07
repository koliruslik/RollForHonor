using RollForHonor.Domain.Combat.Effects.Models;
using RollForHonor.Domain.Combat.State.Models;

namespace RollForHonor.Domain.Combat.State.Changes;

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
