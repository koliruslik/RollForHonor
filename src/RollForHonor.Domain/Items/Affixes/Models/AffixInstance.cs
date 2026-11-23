using RollForHonor.Domain.Items.Models;

namespace RollForHonor.Domain.Items.Affixes.Models;

/// <summary>Retains an affix roll's identity, compatibility and concrete values instead of regenerating them.</summary>
public sealed class AffixInstance
{
    /// <summary>Gets the owned affix identifier, unique within its item.</summary>
    public string Id { get; }

    /// <summary>Gets the stable definition identifier used for this roll.</summary>
    public string DefinitionId { get; }

    /// <summary>Gets the captured exclusivity family.</summary>
    public string FamilyId { get; }

    /// <summary>Gets the captured item compatibility tags.</summary>
    public IReadOnlySet<string> RequiredItemTags { get; }

    /// <summary>Gets the selected tier identifier.</summary>
    public string TierId { get; }

    /// <summary>Gets the d100 quality result, from 1 through 100.</summary>
    public int QualityRoll { get; }

    /// <summary>Gets the actual rolled modifier values; changing content does not change these entries.</summary>
    public IReadOnlyList<ItemModifier> Modifiers { get; }

    /// <summary>Gets the captured tier intervals aligned with the rolled modifiers.</summary>
    public IReadOnlyList<AffixModifierRange> Ranges { get; }

    /// <summary>Gets the captured tier identifiers defined for this affix, independently of the selected roll.</summary>
    public IReadOnlyList<string> AvailableTierIds { get; }

    internal AffixInstance(string id, AffixDefinition definition, string tierId, int qualityRoll,
        IEnumerable<ItemModifier> modifiers)
    {
        Id = id;
        DefinitionId = definition.Id;
        FamilyId = definition.FamilyId;
        RequiredItemTags = definition.RequiredItemTags;
        TierId = tierId;
        QualityRoll = qualityRoll;
        Modifiers = Array.AsReadOnly(modifiers.ToArray());
        Ranges = definition.Tiers.Single(tier => tier.Id == tierId).Modifiers;
        AvailableTierIds = Array.AsReadOnly(definition.Tiers
            .Select(tier => tier.Id)
            .ToArray());
    }
}
