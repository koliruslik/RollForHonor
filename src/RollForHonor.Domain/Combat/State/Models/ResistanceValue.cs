using RollForHonor.Domain.Combat.Damage.Models;

namespace RollForHonor.Domain.Combat.State.Models;

/// <summary>
/// Associates a resistance value with a damage type.
/// </summary>
/// <remarks>
/// DefenseResolver interprets the value as a fractional reduction: 0.25 means
/// 25% less damage, and -0.25 means 25% more damage. It requires values no greater
/// than one and a unique entry per type in the target snapshot. This value object
/// validates the type only; defense-specific constraints are checked by the resolver.
/// </remarks>
public sealed record ResistanceValue
{
    /// <summary>Gets the resisted damage type.</summary>
    public DamageType Type { get; }

    /// <summary>Gets the resistance value used by defense rules.</summary>
    public decimal Value { get; }

    /// <summary>
    /// Creates a validated typed resistance.
    /// </summary>
    /// <param name="type">A defined damage type.</param>
    /// <param name="value">The resistance coefficient consumed by defense rules.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="type"/> is undefined.</exception>
    public ResistanceValue(DamageType type, decimal value)
    {
        if (!Enum.IsDefined(type))
        {
            throw new ArgumentOutOfRangeException(nameof(type));
        }

        Type = type;
        Value = value;
    }

    /// <inheritdoc />
    public override string ToString()
    {
        return $"[{Type}]: {Value}";
    }
}
