using System.Collections.Immutable;

namespace RollForHonor.Domain.Stats;

/// <summary>Owns an immutable, typed attribute-value map with explicit neutral defaults.</summary>
public sealed class StatBlock
{
    private readonly ImmutableDictionary<StatKey, decimal> _values;

    /// <summary>Creates a copied set of unique attribute values.</summary>
    /// <param name="values">Optional unique values; omitted attributes use their neutral defaults.</param>
    /// <exception cref="ArgumentException">Keys are duplicated.</exception>
    public StatBlock(IEnumerable<KeyValuePair<StatKey, decimal>>? values = null)
    {
        _values = values?.ToImmutableDictionary() ?? ImmutableDictionary<StatKey, decimal>.Empty;
    }

    /// <summary>Reads one value without modifying the set.</summary>
    /// <param name="key">The scalar or qualified attribute.</param>
    /// <returns>The stored value, or neutral one for maximum HP, damage, attack-speed and status coefficients, two for critical multiplier, and zero otherwise.</returns>
    public decimal Get(StatKey key) => _values.GetValueOrDefault(key,
        RollForHonor.Domain.Combat.Effects.Models.StatusStatKeys.IsMultiplier(key.Type) ? 1 :
        key.Type == StatType.CriticalMultiplier ? 2 : key.Type == StatType.CharacterLevel ? 1 :
        key.Type is StatType.MaxHealth or StatType.DamageMultiplier or StatType.CriticalMultiplier or StatType.AttackSpeed ? 1 : 0);

    /// <summary>Creates a new base-value set with one entry replaced, preserving this instance.</summary>
    /// <param name="key">The attribute to replace.</param>
    /// <param name="value">The new base value.</param>
    /// <returns>A separate immutable value set.</returns>
    public StatBlock With(StatKey key, decimal value) => new(_values.SetItem(key, value));

}
