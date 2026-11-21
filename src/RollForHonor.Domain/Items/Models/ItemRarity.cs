namespace RollForHonor.Domain.Items.Models;

/// <summary>Identifies affix capacity independently of how many affixes are currently present.</summary>
public enum ItemRarity
{
    /// <summary>An ordinary base with no affix slots under the initial rules.</summary>
    Common,

    /// <summary>The first rarity with craftable affix capacity.</summary>
    Uncommon,

    /// <summary>A base with greater affix capacity than Uncommon.</summary>
    Rare,

    /// <summary>A base with greater affix capacity than Rare.</summary>
    Epic,

    /// <summary>The highest currently defined rarity.</summary>
    Legendary
}
