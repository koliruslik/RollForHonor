namespace RollForHonor.Domain.Combat.Effects;

public abstract record CombatEffectDefinition
{
    public EffectDefinitionId Id { get; }

    protected CombatEffectDefinition(EffectDefinitionId id)
    {
        ArgumentNullException.ThrowIfNull(id);

        Id = id;
    }
}
