using System;
using System.Linq;
using RollForHonor.Domain.Items.Affixes.Models;
using RollForHonor.Domain.Items.Models;
using RollForHonor.Domain.Stats;
using RollForHonor.Godot.UI.Common;
using RollForHonor.Godot.UI.Localization;

namespace RollForHonor.Godot.UI.Inventory;

/// <summary>Shares captured item and affix explanations between item cards and the game reference.</summary>
public static class ItemInspectionTopics
{
    /// <summary>Creates an item overview with its original implicit properties and links to its actual affixes.</summary>
    /// <param name="item">The immutable owned item.</param>
    /// <returns>A rarity-colored explanation without rerolling or looking up replacement values.</returns>
    public static InspectionTopic Item(ItemInstance item)
    {
        var facts = item.Base.Modifiers
            .Select(modifier => new InspectionFact(ItemPresentation.Modifier(modifier), CombatGlossary.StatColor(modifier.Key)))
            .ToList();

        if (item.Base.Weapon is { } weapon)
        {
            facts.Insert(0, new(L.Format($"Attack speed {weapon.AttacksPerSecond:0.##}/s · Mana cost {weapon.ManaCost:0.##}"), HonorTheme.Parchment));
            facts.AddRange(weapon.Damage
                .Select(part => new InspectionFact(L.Damage(part), CombatGlossary.DamageColor(part.Type))));
        }

        var links = item.Base.Modifiers
            .Select(modifier => CombatGlossary.Link(modifier.Key))
            .Concat(item.Affixes
                .Select(affix => new InspectionLink(L.Format($"{ItemPresentation.AffixName(affix.DefinitionId)} · {affix.TierId}"),
                    ItemPresentation.RarityColor(item.Rarity), () => Affix(item.Rarity, affix, true))));

        return new(ItemPresentation.Name(item),
            L.Format($"{item.Rarity} · {item.Base.Footprint.Width}×{item.Base.Footprint.Height} cells\n\nCaptured base properties are listed above. Related affixes show their additional rolled values, ranges and tiers."),
            ItemPresentation.RarityColor(item.Rarity), links, facts);
    }

    /// <summary>Creates the same affix explanation used by item property links.</summary>
    /// <param name="rarity">The owning item's display rarity.</param>
    /// <param name="affix">The immutable rolled affix.</param>
    /// <param name="detailed">Whether to include captured ranges, quality and the available tier pool.</param>
    /// <returns>A linked card retaining the captured affix values.</returns>
    public static InspectionTopic Affix(ItemRarity rarity, AffixInstance affix, bool detailed)
    {
        var facts = affix.Modifiers
            .Select((modifier, index) => new InspectionFact(FormatModifier(affix, modifier, index, detailed), CombatGlossary.StatColor(modifier.Key), true));
        var tiers = detailed
            ? new[] { new InspectionFact(L.Format($"{L.Text("Available tiers: ")}{FormatAvailableTiers(affix)}"), HonorTheme.Muted) }
            : [];
        var links = affix.Modifiers
            .Select(modifier => CombatGlossary.Link(modifier.Key))
            .DistinctBy(link => link.Text);

        return new(L.Format($"{ItemPresentation.AffixName(affix.DefinitionId)} · {affix.TierId}"), "", ItemPresentation.RarityColor(rarity), links, facts.Concat(tiers));
    }

    /// <summary>Formats an actual modifier with optional captured roll details.</summary>
    /// <param name="affix">The modifier's captured affix.</param>
    /// <param name="modifier">The stored value.</param>
    /// <param name="index">The matching range index.</param>
    /// <param name="detailed">Whether to append range, quality percentage and selected tier.</param>
    /// <returns>Plain text with the same units as the item card.</returns>
    public static string FormatModifier(AffixInstance affix, ItemModifier modifier, int index, bool detailed)
    {
        var value = ItemPresentation.Modifier(modifier);

        if (!detailed)
        {
            return value;
        }

        var range = affix.Ranges[index];
        var scale = modifier.Operation != StatOperation.Flat || modifier.Key.Type == StatType.Resistance ? 100 : 1;

        return L.Format($"{value} [{range.Minimum * scale:0.##}/{range.Maximum * scale:0.##}] {affix.EffectiveQualityRoll}% {affix.TierId}");
    }

    /// <summary>Formats the numbered tier bounds as T1 or T1–Tx.</summary>
    /// <param name="affix">The captured affix and available tier identities.</param>
    /// <returns>The T1-based display range; bespoke tier labels use the T1 placeholder.</returns>
    public static string FormatAvailableTiers(AffixInstance affix)
    {
        var maximum = affix.AvailableTierIds
            .Select(id => id.StartsWith("T", StringComparison.Ordinal) && int.TryParse(id.AsSpan(1), out var rank) ? rank : 1)
            .DefaultIfEmpty(1)
            .Max();

        return maximum <= 1 ? "T1" : $"T1–T{maximum}";
    }
}
