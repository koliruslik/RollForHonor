using RollForHonor.Domain.Combat.Damage;

namespace RollForHonor.Domain.Combat.State;

public sealed record ResistanceValue
{
    public DamageType Type { get; }

    public decimal Value { get; }

    public ResistanceValue(DamageType type, decimal value)
    {
        if (!Enum.IsDefined(type))
        {
            throw new ArgumentOutOfRangeException(nameof(type));
        }

        Type = type;
        Value = value;
    }
}
