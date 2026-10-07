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
