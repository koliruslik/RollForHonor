using RollForHonor.Domain.Stats;

namespace RollForHonor.Domain.Items.Models;

/// <summary>Captures released content and level/allocated-attribute equipment requirements.</summary>
/// <param name="Level">Minimum character level; zero permits every starting character.</param>
/// <param name="Attribute">Optional branch attribute.</param>
/// <param name="Value">Minimum allocated attribute value.</param>
/// <param name="Released">Whether the base has approved live balance.</param>
public sealed record EquipmentRequirement(int Level = 0, StatType? Attribute = null, int Value = 0, bool Released = true)
{
    /// <summary>Checks requirements against non-equipment character stats so an item cannot satisfy itself.</summary>
    /// <param name="stats">The resolved non-equipment character profile.</param>
    /// <returns>True when released and every requirement is satisfied.</returns>
    public bool IsMet(StatBlock stats) => Released && stats.Get(new(StatType.CharacterLevel)) >= Level
        && (Attribute is null || stats.Get(new(Attribute.Value)) >= Value);
}
