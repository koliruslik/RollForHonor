using RollForHonor.Domain.Combat.State;

namespace RollForHonor.Domain.Combat.Effects;

public sealed record ResolvedCombatEffect
{
    public CombatantId TargetId { get; }

    public CombatEffectDefinition Definition { get; }

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
