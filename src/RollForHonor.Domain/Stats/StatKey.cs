using RollForHonor.Domain.Combat.Damage.Models;

namespace RollForHonor.Domain.Stats;

/// <summary>Identifies a scalar attribute or a damage-type-specific resistance or multiplier.</summary>
public readonly record struct StatKey
{
    /// <summary>Gets the attribute category.</summary>
    public StatType Type { get; }

    /// <summary>Gets the damage type; null denotes a global attribute.</summary>
    public DamageType? DamageKind { get; }

    /// <summary>Creates a validated attribute key.</summary>
    /// <param name="type">A defined attribute category.</param>
    /// <param name="damageKind">Required for resistance, optional for damage, absent for other attributes.</param>
    /// <exception cref="ArgumentException">The damage qualifier is incompatible with the category.</exception>
    /// <exception cref="ArgumentOutOfRangeException">An enum value is undefined.</exception>
    public StatKey(StatType type, DamageType? damageKind = null)
    {
        if (!Enum.IsDefined(type) || damageKind is { } damage && !Enum.IsDefined(damage))
        {
            throw new ArgumentOutOfRangeException(nameof(type));
        }

        if (type == StatType.Resistance && damageKind is null
            || damageKind is not null && type is not (StatType.Resistance or StatType.DamageMultiplier))
        {
            throw new ArgumentException("The attribute has an incompatible damage qualifier.", nameof(damageKind));
        }

        Type = type;
        DamageKind = damageKind;
    }
}
