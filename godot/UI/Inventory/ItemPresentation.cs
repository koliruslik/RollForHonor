using System.Globalization;
using System.Linq;
using Godot;
using RollForHonor.Domain.Items.Equipment.Configuration;
using RollForHonor.Domain.Items.Equipment.Models;
using RollForHonor.Domain.Items.Models;
using RollForHonor.Domain.Stats;
using RollForHonor.Godot.UI.Localization;

namespace RollForHonor.Godot.UI.Inventory;

/// <summary>Formats actual item data for the game interface without introducing separate stat arithmetic.</summary>
public static class ItemPresentation
{
    /// <summary>Maps current rarity categories to distinguishable text and border colors.</summary>
    /// <param name="rarity">The item's explicit rarity.</param>
    /// <returns>The presentation color.</returns>
    public static Color RarityColor(ItemRarity rarity) => rarity switch
    {
        ItemRarity.Uncommon => new Color("8fb697"),
        ItemRarity.Rare => new Color("78a9c9"),
        ItemRarity.Epic => new Color("b39bd0"),
        ItemRarity.Legendary => new Color("dfb76b"),
        _ => new Color("c5bfae")
    };

    /// <summary>Formats the accepted rarity-specific name while preserving legacy captured base labels.</summary>
    /// <param name="baseId">The base content identity.</param>
    /// <param name="fallback">The captured base name used by legacy content.</param>
    /// <param name="rarity">The item's explicit rarity.</param>
    /// <returns>A localized, gender-correct display name.</returns>
    public static string Name(string baseId, string fallback, ItemRarity rarity)
    {
        var entry = RollForHonor.Godot.Content.Loot.GameLootCatalog.Bases
            .FirstOrDefault(value => value.GetProperty("id").GetString() == baseId);

        return entry.ValueKind != System.Text.Json.JsonValueKind.Undefined && entry.TryGetProperty("rarity_names", out var names)
            ? L.Text(names.GetProperty(rarity.ToString()).GetProperty("en").GetString()) : L.Text(fallback);
    }

    /// <summary>Formats an owned item's rarity-specific name without rerolling its properties.</summary>
    /// <param name="item">The immutable owned item.</param>
    /// <returns>The current localized display name.</returns>
    public static string Name(ItemInstance item) => Name(item.Base.Id, item.Base.Name, item.Rarity);

    /// <summary>Finds the first compatible slot using the active rules.</summary>
    /// <param name="item">The captured item.</param>
    /// <param name="rules">The active slot/tag requirements.</param>
    /// <returns>A compatible slot, or null for an item which is not equipment.</returns>
    public static EquipmentSlot? Slot(ItemInstance item, EquipmentRules rules) => System.Enum.GetValues<EquipmentSlot>()
        .Where(slot => rules.CanEquip(item, slot))
        .Select(slot => (EquipmentSlot?)slot)
        .FirstOrDefault();

    /// <summary>Gets an original vector asset key for a slot.</summary>
    /// <param name="slot">The target slot.</param>
    /// <returns>The icon asset name.</returns>
    public static string Icon(EquipmentSlot slot) => slot switch
    {
        EquipmentSlot.BodyArmor => "body-armor",
        EquipmentSlot.RingLeft or EquipmentSlot.RingRight => "ring",
        EquipmentSlot.OffHand => "offhand",
        _ => slot.ToString().ToLowerInvariant()
    };

    /// <summary>Gets an item-specific hand icon or the compatible equipment category icon.</summary>
    /// <param name="item">The captured item base and category tags.</param>
    /// <param name="slot">The compatible equipment slot, or null for non-equipment.</param>
    /// <returns>The vector asset key, falling back to the bag for non-equipment.</returns>
    public static string Icon(ItemInstance item, EquipmentSlot? slot)
    {
        if (item.Base.Tags.Contains("sword"))
        {
            return "sword";
        }

        if (item.Base.Tags.Contains("wand"))
        {
            return "wand";
        }

        if (item.Base.Tags.Contains("quiver"))
        {
            return "quiver";
        }

        if (item.Base.Tags.Contains("shield"))
        {
            return "shield";
        }

        return slot is { } category ? Icon(category) : "bag";
    }

    /// <summary>Gets a readable slot name.</summary>
    /// <param name="slot">The slot to display.</param>
    /// <returns>A short interface label.</returns>
    public static string SlotName(EquipmentSlot slot) => slot switch
    {
        EquipmentSlot.BodyArmor => L.Text("Body armor"),
        EquipmentSlot.RingLeft => L.Text("Ring I"),
        EquipmentSlot.RingRight => L.Text("Ring II"),
        EquipmentSlot.Weapon => L.Text("Main hand"),
        EquipmentSlot.OffHand => L.Text("Off hand"),
        _ => L.Text(slot.ToString())
    };

    /// <summary>Formats a stored modifier with its actual units and stacking operation.</summary>
    /// <param name="modifier">The concrete base or affix value.</param>
    /// <returns>A signed value and stat label; resistance points and relative increases remain distinct.</returns>
    public static string Modifier(ItemModifier modifier)
    {
        if (DamageDiceStats.Sides(modifier.Key.Type) is var sides && sides > 0)
        {
            return $"+{modifier.Amount:0}d{sides} {modifier.Key.DamageKind} damage";
        }

        var fractional = modifier.Operation != StatOperation.Flat || modifier.Key.Type is StatType.Resistance or StatType.CriticalMultiplier;
        var amount = modifier.Amount * (fractional ? 100 : 1);
        var number = amount.ToString("+0.##;-0.##;0", CultureInfo.InvariantCulture);
        var unit = fractional ? "%" : "";
        var operation = modifier.Operation switch
        {
            StatOperation.Increased => L.Text(" increased"),
            StatOperation.More => L.Text(" more"),
            _ => ""
        };

        return L.Format($"{number}{unit}{operation} {StatName(modifier.Key)}");
    }

    /// <summary>Resolves current catalog names while retaining legacy fixture labels.</summary>
    /// <param name="definitionId">The captured affix definition ID.</param>
    /// <returns>The translated catalog name or a readable legacy label.</returns>
    public static string AffixName(string definitionId)
    {
        var family = RollForHonor.Godot.Content.Loot.GameLootCatalog.Affixes
            .FirstOrDefault(entry => entry.GetProperty("id").GetString() == definitionId);

        return family.ValueKind == System.Text.Json.JsonValueKind.Undefined
            ? L.Text(definitionId.Replace('-', ' ')) : L.Text(family.GetProperty("name_en").GetString());
    }

    private static string StatName(StatKey key)
    {
        var qualifier = key.DamageKind?.ToString().ToLowerInvariant();

        return key.Type switch
        {
            StatType.MaxHealth => L.Text("maximum health"),
            StatType.MaxMana => L.Text("maximum mana"),
            StatType.AttackSpeed => L.Text("attack speed"),
            StatType.MovementSpeed => L.Text("movement speed"),
            StatType.AttackModifier => L.Text("accuracy"),
            StatType.CriticalMultiplier => L.Text("critical damage bonus"),
            StatType.Resistance => L.Format($"{qualifier} resistance"),
            StatType.AddedDamage => L.Format($"added {qualifier} damage"),
            StatType.DamageMultiplier => qualifier is null ? L.Text("all damage") : L.Format($"{qualifier} damage"),
            _ => L.Text(key.Type.ToString().ToLowerInvariant())
        };
    }
}
