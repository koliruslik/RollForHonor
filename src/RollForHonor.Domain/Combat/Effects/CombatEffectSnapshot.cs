namespace RollForHonor.Domain.Combat.Effects;

public sealed record CombatEffectSnapshot
{
    public EffectInstanceId Id { get; }

    public CombatEffectDefinition Definition { get; }

    public CombatEffectSnapshot(
        EffectInstanceId id,
        CombatEffectDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(id);
        ArgumentNullException.ThrowIfNull(definition);

        Id = id;
        Definition = definition;
    }
}
