namespace RollForHonor.Domain.Combat.Effects;

public sealed record EffectInstanceId
{
    public Guid Value { get; }

    public EffectInstanceId(Guid value)
    {
        if (value == Guid.Empty)
        {
            throw new ArgumentException(
                "An effect instance identifier cannot be empty.",
                nameof(value));
        }

        Value = value;
    }
}
