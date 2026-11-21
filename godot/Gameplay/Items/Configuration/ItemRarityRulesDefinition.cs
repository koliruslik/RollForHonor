using Godot;
using RollForHonor.Domain.Items.Configuration;
using RollForHonor.Domain.Items.Models;

namespace RollForHonor.Godot.Gameplay.Items.Configuration;

/// <summary>Exposes editable rarity capacities and copies them into engine-independent immutable rules.</summary>
[GlobalClass]
public partial class ItemRarityRulesDefinition : Resource
{
    /// <summary>Gets or sets the affix capacity of ordinary items.</summary>
    [Export(PropertyHint.Range, "0,64,1,or_greater")]
    public int CommonCapacity { get; set; } = ItemRarityRules.Default.GetCapacity(ItemRarity.Common);

    /// <summary>Gets or sets the affix capacity of uncommon items.</summary>
    [Export(PropertyHint.Range, "0,64,1,or_greater")]
    public int UncommonCapacity { get; set; } = ItemRarityRules.Default.GetCapacity(ItemRarity.Uncommon);

    /// <summary>Gets or sets the affix capacity of rare items.</summary>
    [Export(PropertyHint.Range, "0,64,1,or_greater")]
    public int RareCapacity { get; set; } = ItemRarityRules.Default.GetCapacity(ItemRarity.Rare);

    /// <summary>Gets or sets the affix capacity of epic items.</summary>
    [Export(PropertyHint.Range, "0,64,1,or_greater")]
    public int EpicCapacity { get; set; } = ItemRarityRules.Default.GetCapacity(ItemRarity.Epic);

    /// <summary>Gets or sets the affix capacity of legendary items.</summary>
    [Export(PropertyHint.Range, "0,64,1,or_greater")]
    public int LegendaryCapacity { get; set; } = ItemRarityRules.Default.GetCapacity(ItemRarity.Legendary);

    /// <summary>Captures editor values; later Resource edits do not mutate the resulting Domain rules.</summary>
    /// <returns>A complete immutable table of rarity capacities.</returns>
    /// <exception cref="System.ArgumentException">A configured capacity is negative.</exception>
    public ItemRarityRules CreateRules() => new(
    [
        new(ItemRarity.Common, CommonCapacity),
        new(ItemRarity.Uncommon, UncommonCapacity),
        new(ItemRarity.Rare, RareCapacity),
        new(ItemRarity.Epic, EpicCapacity),
        new(ItemRarity.Legendary, LegendaryCapacity)
    ]);
}
