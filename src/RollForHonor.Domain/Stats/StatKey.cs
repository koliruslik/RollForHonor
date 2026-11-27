using RollForHonor.Domain.Combat.Damage.Models;

namespace RollForHonor.Domain.Stats;

/// <summary>Identifies a scalar attribute or damage-type-specific resistance, flat damage, extra dice or coefficient.</summary>
public readonly record struct StatKey
{
    /// <summary>Gets the attribute category.</summary>
    public StatType Type { get; }

    /// <summary>Gets the damage type; null denotes a global attribute.</summary>
    public DamageType? DamageKind { get; }

    /// <summary>Creates a validated attribute key.</summary>
    /// <param name="type">A defined attribute category.</param>
    /// <param name="damageKind">Required for resistance, flat damage and extra dice, optional for damage coefficients, absent for other attributes.</param>
    /// <exception cref="ArgumentException">The damage qualifier is incompatible with the category.</exception>
    /// <exception cref="ArgumentOutOfRangeException">An enum value is undefined.</exception>
    public StatKey(StatType type, DamageType? damageKind = null)
    {
        if (damageKind == DamageType.Pure)
        {
            throw new ArgumentException("Pure periodic damage has no equipment or resistance stats.", nameof(damageKind));
        }

        if (!Enum.IsDefined(type) || damageKind is { } damage && !Enum.IsDefined(damage))
        {
            throw new ArgumentOutOfRangeException(nameof(type));
        }

        if ((type is StatType.Resistance or StatType.AddedDamage || DamageDiceStats.Sides(type) > 0) && damageKind is null
            || damageKind is not null && type is not (StatType.Resistance or StatType.DamageMultiplier or StatType.AddedDamage) && DamageDiceStats.Sides(type) == 0)
        {
            throw new ArgumentException("The attribute has an incompatible damage qualifier.", nameof(damageKind));
        }

        Type = type;
        DamageKind = damageKind;
    }
}
