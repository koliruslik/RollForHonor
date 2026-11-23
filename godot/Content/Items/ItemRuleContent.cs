using System;
using Godot;
using RollForHonor.Domain.Items.Configuration;
using RollForHonor.Godot.Gameplay.Items.Configuration;

namespace RollForHonor.Godot.Content.Items;

/// <summary>Loads the shared editable rarity table used by item creation and presentation.</summary>
public static class ItemRuleContent
{
    /// <summary>Captures the current content Resource into immutable Domain rules.</summary>
    /// <returns>The configured capacities, independent of later Resource edits.</returns>
    /// <exception cref="InvalidOperationException">The authored Resource cannot be loaded.</exception>
    public static ItemRarityRules LoadRarityRules()
    {
        using var definition = ResourceLoader.Load<ItemRarityRulesDefinition>("res://Items/ItemRarityRules.tres",
            cacheMode: ResourceLoader.CacheMode.Ignore)
            ?? throw new InvalidOperationException("Rarity rules content could not be loaded.");

        return definition.CreateRules();
    }
}
