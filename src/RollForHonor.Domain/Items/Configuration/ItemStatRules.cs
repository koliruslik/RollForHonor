using System.Collections.Immutable;
using RollForHonor.Domain.Items.Models;
using RollForHonor.Domain.Stats;

namespace RollForHonor.Domain.Items.Configuration;

/// <summary>Restricts operations in item content without restricting base character definitions.</summary>
public sealed class ItemStatRules
{
    /// <summary>Gets initial rules: flat attack bonuses, percentage-only damage/crit/movement bonuses, both elsewhere.</summary>
    public static ItemStatRules Default { get; } = new(Enum.GetValues<StatType>()
        .Where(type => type != StatType.CharacterLevel)
        .Select(type => new KeyValuePair<StatType, IEnumerable<StatOperation>>(type, GetDefaultOperations(type))));

    private readonly ImmutableDictionary<StatType, ImmutableHashSet<StatOperation>> _operations;

    /// <summary>Copies the permitted operations for item-modifiable stat categories.</summary>
    /// <param name="operations">Unique categories with nonempty collections of defined operations; absent categories are forbidden.</param>
    /// <exception cref="ArgumentNullException">The table is null.</exception>
    /// <exception cref="ArgumentException">Categories/operations are invalid, duplicated or empty.</exception>
    public ItemStatRules(IEnumerable<KeyValuePair<StatType, IEnumerable<StatOperation>>> operations)
    {
        ArgumentNullException.ThrowIfNull(operations);

        var entries = operations.ToArray();

        if (entries.Any(pair => pair.Value is null))
        {
            throw new ArgumentException("Allowed operations cannot be null.", nameof(operations));
        }

        _operations = entries.ToImmutableDictionary(pair => pair.Key, pair => pair.Value.ToImmutableHashSet());

        if (_operations.Any(pair => !Enum.IsDefined(pair.Key) || pair.Value.Count == 0
            || pair.Value.Any(operation => !Enum.IsDefined(operation))))
        {
            throw new ArgumentException("Item stat rules require defined categories and operations.", nameof(operations));
        }
    }

    /// <summary>Checks whether an operation is enabled for a stat category in item content.</summary>
    /// <param name="type">The affected stat category.</param>
    /// <param name="operation">The requested stacking operation.</param>
    /// <returns>True when the configured operation is permitted.</returns>
    public bool Allows(StatType type, StatOperation operation) =>
        _operations.TryGetValue(type, out var allowed) && allowed.Contains(operation);

    /// <summary>Rejects a modifier that cannot appear on an item under these rules.</summary>
    /// <param name="modifier">The immutable modifier value to check.</param>
    /// <exception cref="ArgumentNullException">The value is null.</exception>
    /// <exception cref="ArgumentException">The operation is forbidden for its stat category.</exception>
    public void Validate(ItemModifier modifier)
    {
        ArgumentNullException.ThrowIfNull(modifier);

        if (!Allows(modifier.Key.Type, modifier.Operation))
        {
            throw new ArgumentException("This operation is not permitted for the item's stat category.", nameof(modifier));
        }
    }

    private static StatOperation[] GetDefaultOperations(StatType type) => type switch
    {
        StatType.AttackModifier or StatType.CriticalRollBonus or StatType.Strength or StatType.Dexterity or StatType.Constitution
            or StatType.Intelligence or StatType.Luck or StatType.HealthRecovery or StatType.ManaRecovery => [StatOperation.Flat],
        StatType.MovementSpeed or StatType.AttackSpeed or StatType.CriticalChance or StatType.CriticalMultiplier
            or StatType.DamageMultiplier => [StatOperation.Increased, StatOperation.More],
        _ => [StatOperation.Flat, StatOperation.Increased, StatOperation.More]
    };
}
