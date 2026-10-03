using RollForHonor.Domain.Combat.Damage;

namespace RollForHonor.Domain.Combat.State;

/// <summary>
/// Associates a resistance value with a damage type.
/// </summary>
public sealed record ResistanceValue
{
    /// <summary>Gets the resisted damage type.</summary>
    public DamageType Type { get; }

    /// <summary>Gets the resistance value used by defense rules.</summary>
    public decimal Value { get; }

    /// <summary>
    /// Creates a validated typed resistance.
    /// </summary>
    public ResistanceValue(DamageType type, decimal value)
    {
        if (!Enum.IsDefined(type))
        {
            throw new ArgumentOutOfRangeException(nameof(type));
        }

        Type = type;
        Value = value;
    }

    public override string ToString()
    {
        return $"[{Type}]: {Value}";
    }
}
