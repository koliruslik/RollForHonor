namespace RollForHonor.Domain.Stats;

/// <summary>Identifies typed extra-damage dice in the resolved stat model.</summary>
public static class DamageDiceStats
{
    /// <summary>Gets all supported dice-count categories.</summary>
    public static IReadOnlyList<StatType> Types { get; } = Array.AsReadOnly(new[]
    {
        StatType.AddedDamageD4, StatType.AddedDamageD6, StatType.AddedDamageD8,
        StatType.AddedDamageD10, StatType.AddedDamageD12
    });

    /// <summary>Reads a dice category's face count.</summary>
    /// <param name="type">The category to inspect.</param>
    /// <returns>The face count, or zero for a nondice stat.</returns>
    public static int Sides(StatType type) => type switch
    {
        StatType.AddedDamageD4 => 4,
        StatType.AddedDamageD6 => 6,
        StatType.AddedDamageD8 => 8,
        StatType.AddedDamageD10 => 10,
        StatType.AddedDamageD12 => 12,
        _ => 0
    };
}
