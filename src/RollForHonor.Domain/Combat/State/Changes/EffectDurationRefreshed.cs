using RollForHonor.Domain.Combat.Effects.Models;
using RollForHonor.Domain.Combat.State.Models;

namespace RollForHonor.Domain.Combat.State.Changes;

/// <summary>Records a committed duration refresh without replacing the active effect or its captured damage and slow.</summary>
public sealed record EffectDurationRefreshed : ICombatStateChange
{
    /// <summary>Gets the affected recipient.</summary>
    public CombatantId CombatantId { get; }

    /// <summary>Gets the existing instance whose duration is refreshed.</summary>
    public EffectInstanceId EffectId { get; }

    /// <summary>Creates a duration-only refresh proposal.</summary>
    /// <param name="combatantId">The current recipient.</param>
    /// <param name="effectId">An active periodic instance.</param>
    /// <exception cref="ArgumentNullException">An identity is null.</exception>
    public EffectDurationRefreshed(CombatantId combatantId, EffectInstanceId effectId)
    {
        CombatantId = combatantId ?? throw new ArgumentNullException(nameof(combatantId));
        EffectId = effectId ?? throw new ArgumentNullException(nameof(effectId));
    }
}
