namespace RollForHonor.Domain.Combat.Effects;

public sealed record EffectDefinitionId
{
    public Guid Value { get; }

    public EffectDefinitionId(Guid value)
    {
        if (value == Guid.Empty)
        {
            throw new ArgumentException(
                "An effect definition identifier cannot be empty.",
                nameof(value));
        }

        Value = value;
    }
}
