using RollForHonor.Domain.Items.Affixes.Models;

namespace RollForHonor.Domain.Items.Models;

/// <summary>Owns a stable base snapshot, explicit rarity and immutable rolled affixes.</summary>
public sealed class ItemInstance
{
    /// <summary>Gets the identifier allocated by the caller, independent of random stat rolls.</summary>
    public string Id { get; }

    /// <summary>Gets the immutable base captured when the item was created.</summary>
    public ItemDefinition Base { get; }

    /// <summary>Gets the explicit capacity category; removing affixes does not lower it.</summary>
    public ItemRarity Rarity { get; }

    /// <summary>Gets the immutable level used for affix gates, independent of display score and equip requirements.</summary>
    public int GenerationLevel { get; }

    /// <summary>Gets the occupied affix slots and their stored values.</summary>
    public IReadOnlyList<AffixInstance> Affixes { get; }

    internal ItemInstance(string id, ItemDefinition definition, ItemRarity rarity, AffixInstance[] affixes, int generationLevel = 1)
    {
        GenerationLevel = generationLevel;
        Id = id;
        Base = definition;
        Rarity = rarity;
        Affixes = Array.AsReadOnly(affixes);
    }
}
