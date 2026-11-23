using RollForHonor.Domain.Combat.Damage.Models;

namespace RollForHonor.Domain.Stats;

/// <summary>Resolves (base + flat) times additive Increased times independent More factors without mutating inputs.</summary>
public sealed class StatCalculator
{
    /// <summary>Calculates every currently defined attribute and all damage-qualified values.</summary>
    /// <param name="profile">The immutable base definition and active modifiers.</param>
    /// <returns>A complete immutable set; typed outgoing coefficients already combine global and typed modifiers.</returns>
    /// <exception cref="ArgumentNullException">The profile is null.</exception>
    /// <exception cref="OverflowException">Decimal arithmetic exceeds its representable range.</exception>
    public StatBlock Calculate(StatProfile profile)
    {
        ArgumentNullException.ThrowIfNull(profile);

        var keys = Enum.GetValues<StatType>()
            .Where(type => type is not (StatType.Resistance or StatType.AddedDamage))
            .Select(type => new StatKey(type))
            .Concat(Enum.GetValues<DamageType>()
                .SelectMany(damage => new[] { new StatKey(StatType.Resistance, damage), new StatKey(StatType.DamageMultiplier, damage), new StatKey(StatType.AddedDamage, damage) }));

        return new StatBlock(keys.Select(key => new KeyValuePair<StatKey, decimal>(key, CalculateValue(profile, key))));
    }

    private static decimal CalculateValue(StatProfile profile, StatKey key)
    {
        var global = new StatKey(key.Type, key.Type is StatType.Resistance or StatType.AddedDamage ? key.DamageKind : null);
        var combineDamage = key.Type == StatType.DamageMultiplier && key.DamageKind is not null;
        var entries = profile.Modifiers
            .Where(modifier => modifier.Key == key || combineDamage && modifier.Key == global)
            .ToArray();
        var baseValue = profile.BaseValues.Get(key);

        if (combineDamage)
        {
            baseValue = Math.Max(0, baseValue) * Math.Max(0, profile.BaseValues.Get(global));
        }

        var flat = entries
            .Where(entry => entry.Operation == StatOperation.Flat)
            .Select(entry => entry.Amount)
            .Order()
            .Sum();
        var increased = entries
            .Where(entry => entry.Operation == StatOperation.Increased)
            .Select(entry => entry.Amount)
            .Order()
            .Sum();
        var more = entries
            .Where(entry => entry.Operation == StatOperation.More)
            .Select(entry => 1 + entry.Amount)
            .Order()
            .Aggregate(1m, (product, factor) => product * factor);
        var result = (baseValue + flat) * Math.Max(0, 1 + increased) * more;

        return key.Type switch
        {
            StatType.Resistance => Math.Min(1, result),
            StatType.CriticalChance => Math.Clamp(result, 0, 1),
            StatType.MaxHealth => Math.Max(1, result),
            StatType.AttackModifier => result,
            _ => Math.Max(0, result)
        };
    }
}
