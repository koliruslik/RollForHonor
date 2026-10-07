namespace RollForHonor.Domain.Combat.Damage.Models;

/// <summary>
/// Associates a non-negative amount of damage with its type.
/// </summary>
public sealed record DamageAmount
{
    /// <summary>Gets the damage type.</summary>
    public DamageType Type { get; }

    /// <summary>Gets the non-negative damage amount.</summary>
    public int Amount { get; }

    /// <summary>
    /// Creates a validated typed damage amount.
    /// </summary>
    /// <param name="type">A defined damage type.</param>
    /// <param name="amount">A non-negative integer amount.</param>
    /// <exception cref="ArgumentOutOfRangeException">The type is undefined or amount is negative.</exception>
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

    /// <inheritdoc />
    public override string ToString()
    {
        return $"{Amount} [{Type}]";
    }
}
