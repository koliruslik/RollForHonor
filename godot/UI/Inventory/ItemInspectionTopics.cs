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
        var facts = RollForHonor.Domain.Items.Services.ItemBaseScaling.Modifiers(item)
            .Select(modifier => new InspectionFact(ItemPresentation.Modifier(modifier), CombatGlossary.StatColor(modifier.Key)))
            .ToList();

        if (item.Base.Weapon is { } weapon)
        {
            facts.Insert(0, new(L.Format($"Attack speed {weapon.AttacksPerSecond:0.##}/s · Mana cost {weapon.ManaCost:0.##}"), HonorTheme.Parchment));
            facts.AddRange(RollForHonor.Domain.Items.Services.ItemBaseScaling.WeaponDamage(item)
                .SelectMany(part => new[]
                {
                    new InspectionFact(ItemPresentation.WeaponDamageRange(part), CombatGlossary.DamageColor(part.Type)),
                    new InspectionFact(ItemPresentation.WeaponDamageFormula(part), CombatGlossary.DamageColor(part.Type))
                }));
        }

        var links = item.Base.Modifiers
            .Select(modifier => CombatGlossary.Link(modifier.Key))
            .Concat(item.Affixes
                .Select(affix => new InspectionLink(ItemPresentation.AffixName(affix.DefinitionId),
                    ItemPresentation.RarityColor(item.Rarity), () => Affix(item.Rarity, affix, false))));

        return new(ItemPresentation.Name(item),
            L.Format($"{item.Rarity} · {item.Base.Footprint.Width}×{item.Base.Footprint.Height} cells\n\nCaptured base properties are listed above. Related affixes show their additional rolled values, ranges and tiers."),
            ItemPresentation.RarityColor(item.Rarity), links, facts);
    }

    /// <summary>Creates the same affix explanation used by item property links.</summary>
    /// <param name="rarity">The owning item's display rarity.</param>
    /// <param name="affix">The immutable rolled affix.</param>
    /// <param name="detailed">Whether to include captured ranges, quality and the current tier.</param>
    /// <returns>A linked card retaining the captured affix values.</returns>
    public static InspectionTopic Affix(ItemRarity rarity, AffixInstance affix, bool detailed)
    {
        var facts = affix.Modifiers
            .Where((_, index) => GroupLabel(affix) is null || index == 0)
            .Select((modifier, index) => new InspectionFact(FormatModifier(affix, modifier, index, detailed), CombatGlossary.StatColor(modifier.Key), true));
        var links = affix.Modifiers
            .Select(modifier => CombatGlossary.Link(modifier.Key))
            .DistinctBy(link => link.Text);

        var title = ItemPresentation.AffixName(affix.DefinitionId);
        return new(detailed ? L.Format($"{title} · {affix.TierId}") : title, "", ItemPresentation.RarityColor(rarity), links, facts);
    }

    /// <summary>Formats an actual modifier with optional captured roll details.</summary>
    /// <param name="affix">The modifier's captured affix.</param>
    /// <param name="modifier">The stored value.</param>
    /// <param name="index">The matching range index.</param>
    /// <param name="detailed">Whether to append range, quality percentage and selected tier.</param>
    /// <returns>Plain text with the same units as the item card.</returns>
    public static string FormatModifier(AffixInstance affix, ItemModifier modifier, int index, bool detailed)
    {
        var value = ItemPresentation.CompactModifier(modifier, GroupLabel(affix));

        if (!detailed)
        {
            return value;
        }

        var range = affix.Ranges[index];
        var status = StatusEffectPresentation.ForStat(modifier.Key.Type);
        var scale = modifier.Operation != StatOperation.Flat || status?.Stats.Guaranteed == modifier.Key.Type
            || modifier.Key.Type is StatType.Resistance or StatType.PoisonChance or StatType.IgniteChance or StatType.BleedChance ? 100 : 1;

        return L.Format($"{value} [{range.Minimum * scale:0.##}/{range.Maximum * scale:0.##}] {affix.EffectiveQualityRoll}% {affix.TierId}");
    }

    internal static string GroupLabel(AffixInstance affix)
    {
        if (affix.Modifiers.Count < 2 || affix.Modifiers.Any(value => value.Amount != affix.Modifiers[0].Amount
            || value.Operation != affix.Modifiers[0].Operation))
        {
            return null;
        }

        var label = affix.DefinitionId switch
        {
            "added-physical" or "increased-physical" => "physical damage",
            "added-elemental" or "increased-elemental" => "elemental damage",
            "global-damage" => "all damage",
            "all-attributes" => "all attributes",
            "resist-physical" => "physical resistance",
            "resist-elemental" => "elemental resistance",
            "resist-all" => "all resistances",
            _ => null
        };
        return label is null ? null : L.Text(label);
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
