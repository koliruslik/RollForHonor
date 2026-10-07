using RollForHonor.Domain.Combat.State.Models;

namespace RollForHonor.Domain.Combat.Effects.Models;

/// <summary>
/// Describes an effect selected for a target during combat resolution.
/// </summary>
public sealed record ResolvedCombatEffect
{
    /// <summary>Gets the identifier of the selected target.</summary>
    public CombatantId TargetId { get; }

    /// <summary>Gets the effect selected for the target.</summary>
    public CombatEffectDefinition Definition { get; }

    /// <summary>
    /// Creates a resolved effect for the supplied target.
    /// </summary>
    /// <param name="targetId">The combatant selected to receive the effect.</param>
    /// <param name="definition">The resolved effect definition.</param>
    /// <exception cref="ArgumentNullException">Either argument is null.</exception>
    public ResolvedCombatEffect(
        CombatantId targetId,
        CombatEffectDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(targetId);
        ArgumentNullException.ThrowIfNull(definition);

        TargetId = targetId;
        Definition = definition;
    }
}
