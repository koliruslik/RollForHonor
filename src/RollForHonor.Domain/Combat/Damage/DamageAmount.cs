namespace RollForHonor.Domain.Combat.Damage;

public sealed record DamageAmount
{
    public DamageType Type { get; }

    public int Amount { get; }

    public DamageAmount(DamageType type, int amount)
    {
        if (!Enum.IsDefined(type))
        {
            throw new ArgumentOutOfRangeException(nameof(type));
        }

        ArgumentOutOfRangeException.ThrowIfNegative(amount);

        Type = type;
        Amount = amount;
    }
}
