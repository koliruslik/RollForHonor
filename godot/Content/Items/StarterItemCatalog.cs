using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Godot;
using RollForHonor.Domain.Combat.Damage.Models;
using RollForHonor.Domain.Combat.State.Models;
using RollForHonor.Domain.Dice.Models;
using RollForHonor.Domain.Dice.Services;
using RollForHonor.Domain.Items.Affixes.Models;
using RollForHonor.Domain.Items.Affixes.Services;
using RollForHonor.Domain.Items.Configuration;
using RollForHonor.Domain.Items.Models;
using RollForHonor.Domain.Items.Services;
using RollForHonor.Domain.Stats;

namespace RollForHonor.Godot.Content.Items;

/// <summary>Loads a small authored starting kit into the existing Domain item model without implementing drops or a crafting economy.</summary>
public static class StarterItemCatalog
{
    /// <summary>Creates independent owned instances from editable JSON, retaining actual affix values.</summary>
    /// <param name="owner">The actor identity used to namespace instance IDs across shared test/session hosts.</param>
    /// <returns>A copied starting kit; no items are automatically equipped.</returns>
    /// <exception cref="JsonException">The content is malformed.</exception>
    /// <exception cref="ArgumentNullException">The owner identity is null.</exception>
    /// <exception cref="ArgumentException">Content violates Domain item/stat rules.</exception>
    public static IReadOnlyList<ItemInstance> Create(CombatantId owner)
    {
        ArgumentNullException.ThrowIfNull(owner);
        using var json = JsonDocument.Parse(FileAccess.GetFileAsString("res://Content/Items/starter-items.json"));
        var factory = new ItemFactory(ItemRuleContent.LoadRarityRules(), ItemStatRules.Default);

        return json.RootElement.EnumerateArray()
            .Select(entry => CreateItem(entry, owner, factory))
            .ToArray();
    }

    private static ItemInstance CreateItem(JsonElement entry, CombatantId owner, ItemFactory factory)
    {
        var id = entry.GetProperty("id").GetString();
        var definition = new ItemDefinition(id, entry.GetProperty("name").GetString(),
            ReadTags(entry), entry.GetProperty("modifiers").EnumerateArray()
                .Select(modifier => new ItemModifier(Key(modifier), Operation(modifier), modifier.GetProperty("value").GetDecimal())), ReadWeapon(entry), ReadFootprint(entry));
        var affixes = entry.GetProperty("affixes").EnumerateArray()
            .Select(affix => CreateAffix(affix, id));

        return factory.Create($"{owner.Value:N}:{id}", definition,
            Enum.Parse<ItemRarity>(entry.GetProperty("rarity").GetString()), affixes);
    }

    private static IEnumerable<string> ReadTags(JsonElement entry) => entry.TryGetProperty("tags", out var tags)
        ? tags.EnumerateArray()
            .Select(tag => tag.GetString())
        : [entry.GetProperty("tag").GetString()];

    private static WeaponProperties ReadWeapon(JsonElement entry)
    {
        if (!entry.TryGetProperty("weapon", out var weapon))
        {
            return null;
        }

        return new WeaponProperties(weapon.GetProperty("attacksPerSecond").GetDecimal(), weapon.GetProperty("manaCost").GetDecimal(),
            weapon.GetProperty("damage").EnumerateArray()
                .Select(part => new DamageComponent(new DiceFormula(part.GetProperty("count").GetInt32(), part.GetProperty("sides").GetInt32()),
                    part.GetProperty("flat").GetInt32(), Enum.Parse<DamageType>(part.GetProperty("type").GetString()))));
    }

    private static ItemFootprint ReadFootprint(JsonElement entry) => entry.TryGetProperty("inventorySize", out var size)
        ? new ItemFootprint(size.GetProperty("width").GetInt32(), size.GetProperty("height").GetInt32())
        : ItemFootprint.SingleCell;

    private static StatKey Key(JsonElement modifier) => new(Enum.Parse<StatType>(modifier.GetProperty("type").GetString()),
        modifier.TryGetProperty("damage", out var damage) ? Enum.Parse<DamageType>(damage.GetString()) : null);

    private static StatOperation Operation(JsonElement modifier) => Enum.Parse<StatOperation>(modifier.GetProperty("operation").GetString());

    private static AffixInstance CreateAffix(JsonElement entry, string itemId)
    {
        var id = entry.GetProperty("id").GetString();
        var tier = entry.GetProperty("tier").GetString();
        var ranges = entry.GetProperty("modifiers").EnumerateArray()
            .Select(modifier => new AffixModifierRange(Key(modifier), Operation(modifier),
                modifier.GetProperty("minimum").GetDecimal(), modifier.GetProperty("maximum").GetDecimal(),
                modifier.GetProperty("precision").GetInt32()));
        var definition = new AffixDefinition(id, id, 1, [new AffixTier(tier, 0, ranges)]);

        return new AffixRoller().Roll($"{itemId}:{id}", definition, tier, new AuthoredQuality(entry.GetProperty("quality").GetInt32()));
    }

    private sealed class AuthoredQuality(int quality) : IDiceRoller
    {
        /// <inheritdoc />
        public DiceTrace Trace => new(_rolls);

        private readonly List<DiceRoll> _rolls = [];

        /// <inheritdoc />
        public DiceRoll Roll(DiceFormula formula)
        {
            var roll = new DiceRoll(formula, [quality], quality);
            _rolls.Add(roll);

            return roll;
        }
    }
}
