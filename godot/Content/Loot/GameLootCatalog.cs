using System;
using System.Collections.Generic;
using System.Globalization;
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
using RollForHonor.Domain.Rewards;
using RollForHonor.Domain.Stats;
using RollForHonor.Godot.Content.Items;

namespace RollForHonor.Godot.Content.Loot;

/// <summary>Loads the accepted live loot, base and affix tables, shared with the reference browser.</summary>
public static class GameLootCatalog
{
    /// <summary>Gets immutable JSON snapshots of all registered monster reward profiles.</summary>
    public static IReadOnlyList<JsonElement> Monsters { get; } = Load("monsters");

    /// <summary>Gets immutable JSON snapshots of all base definitions, including level gates.</summary>
    public static IReadOnlyList<JsonElement> Bases { get; } = Load("bases");

    /// <summary>Gets all accepted families, including explicitly disabled future mechanics.</summary>
    public static IReadOnlyList<JsonElement> Affixes { get; } = Load("affixes");

    /// <summary>Generates a reproducible compatibility kit for fixtures; playable runs choose one weapon instead.</summary>
    /// <param name="owner">The owner used to namespace instance IDs.</param>
    /// <returns>Twelve independently captured items with current base stats and affixes.</returns>
    /// <exception cref="ArgumentNullException">The owner identity is null.</exception>
    /// <exception cref="ArgumentException">Live item content violates Domain rules.</exception>
    /// <exception cref="InvalidOperationException">A starting base or eligible affix pool is invalid.</exception>
    public static IReadOnlyList<ItemInstance> CreateStarterKit(CombatantId owner)
    {
        ArgumentNullException.ThrowIfNull(owner);
        var dice = new RollForHonor.Infrastructure.Dice.RandomDiceRoller(new Random(20261125));

        return Bases
            .Where(entry => entry.TryGetProperty("starter_rarity", out _))
            .Select(entry => CreateItem(entry.GetProperty("id").GetString(), $"{owner.Value:N}:{entry.GetProperty("id").GetString()}",
                Enum.Parse<ItemRarity>(entry.GetProperty("starter_rarity").GetString()), dice))
            .ToArray();
    }

    /// <summary>Rolls independent gold and equipment groups once for a defeated reward-bearing actor.</summary>
    /// <param name="monsterId">The registered monster profile ID.</param>
    /// <param name="source">The defeated actor identity used for unique item instances.</param>
    /// <param name="dice">Caller-owned randomness; reference browsing never calls this method.</param>
    /// <param name="luck">Captured player Luck for occurrence, rarity, gold quantity and affix rolls.</param>
    /// <exception cref="ArgumentNullException">The source identity or dice source is null.</exception>
    /// <exception cref="ArgumentException">Live item content violates Domain rules.</exception>
    /// <exception cref="OverflowException">Weights or item values exceed their numeric range.</exception>
    /// <returns>Captured group rolls and an optional generated item, suitable for placement retries.</returns>
    /// <exception cref="InvalidOperationException">The profile is unknown or its gated base/affix pool is insufficient.</exception>
    public static MonsterLoot Roll(string monsterId, CombatantId source, IDiceRoller dice, decimal luck)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(dice);
        var monster = Monsters.Single(entry => entry.GetProperty("id").GetString() == monsterId);
        var gold = monster.GetProperty("gold");
        var goldGroup = new LootGroupDefinition("gold", new(1, 100),
            [new(1, LootOutcome.GoldEntry(new GoldRange(gold.GetProperty("minimum").GetInt64(), gold.GetProperty("maximum").GetInt64())))]);
        var roller = new LootGroupRoller();
        var goldRoll = roller.Roll([goldGroup], 0, dice, luck).Single();
        var equipment = roller.Roll([EquipmentGroup(monsterId, luck)], 0, dice, luck).Single();

        if (equipment.Outcome.IsEmpty)
        {
            return new(goldRoll, equipment, null);
        }

        var slot = Weighted(monster.GetProperty("slots").EnumerateArray(), dice).GetProperty("slot").GetString();
        var level = monster.GetProperty("item_level").GetInt32();
        var pool = Bases
            .Where(entry => entry.GetProperty("loot_enabled").GetBoolean())
            .Where(entry => entry.GetProperty("slot").GetString() == slot && entry.GetProperty("item_level").GetInt32() <= level)
            .ToArray();
        var selected = Weighted(pool, dice);
        var item = CreateItem(selected.GetProperty("id").GetString(), $"{source.Value:N}:equipment", equipment.Outcome.Rarity.Value,
            dice, luck, monster.GetProperty("occupied_slot_percent").GetInt32(), level);

        return new(goldRoll, equipment, item);
    }

    /// <summary>Builds the exact single-roll occurrence/rarity table used by rewards and the reference.</summary>
    /// <param name="monsterId">The registered monster reward profile.</param>
    /// <param name="luck">Captured player Luck; negative values behave as zero.</param>
    /// <returns>Ordered thresholds on the configured precision die, with no randomness consumed.</returns>
    /// <exception cref="InvalidOperationException">The monster is unknown.</exception>
    /// <exception cref="ArgumentException">The configured base distribution or resolution is invalid.</exception>
    /// <exception cref="ArgumentOutOfRangeException">The configured Luck scale or resolution is invalid.</exception>
    public static LootGroupDefinition EquipmentGroup(string monsterId, decimal luck = 0)
    {
        var monster = Monsters.Single(entry => entry.GetProperty("id").GetString() == monsterId);
        var threshold = 1;
        var bands = new List<LootBand>();

        foreach (var entry in monster.GetProperty("equipment_outcomes").EnumerateArray())
        {
            var rarity = entry.GetProperty("rarity").GetString();
            bands.Add(new(threshold, rarity == "none" ? LootOutcome.Nothing() : LootOutcome.EquipmentEntry(monsterId, Enum.Parse<ItemRarity>(rarity))));
            threshold += entry.GetProperty("weight").GetInt32();
        }

        var group = new LootGroupDefinition("equipment", new(1, threshold - 1), bands);
        return new LootLuckPolicy(monster.GetProperty("equipment_luck_scale").GetDecimal(),
            monster.GetProperty("equipment_roll_sides").GetInt32()).Apply(group, new(luck));
    }

    /// <summary>Creates a current base at an explicit rarity using current weighted families and retained rolls.</summary>
    /// <param name="baseId">The registered base ID.</param>
    /// <param name="instanceId">The unique owned or world identity.</param>
    /// <param name="rarity">Explicit rarity, independent of occupied affix count.</param>
    /// <param name="dice">Caller-owned randomness.</param>
    /// <param name="luck">Captured nonnegative Luck policy input.</param>
    /// <param name="occupancyPercent">Independent chance that each rarity affix slot is occupied.</param>
    /// <param name="itemLevel">Optional monster item level; omitted uses the base's minimum level.</param>
    /// <returns>An immutable item with all actual affix values captured.</returns>
    /// <exception cref="ArgumentNullException">The dice source is null.</exception>
    /// <exception cref="ArgumentException">The identity, rarity or live item content violates Domain rules.</exception>
    /// <exception cref="OverflowException">Weights or item values exceed their numeric range.</exception>
    /// <exception cref="InvalidOperationException">The base is unknown or the eligible family pool is insufficient.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Occupancy is outside 0–100 or item level is below the base gate.</exception>
    public static ItemInstance CreateItem(string baseId, string instanceId, ItemRarity rarity, IDiceRoller dice,
        decimal luck = 0, int occupancyPercent = 55, int? itemLevel = null)
    {
        ArgumentNullException.ThrowIfNull(dice);
        ArgumentOutOfRangeException.ThrowIfLessThan(occupancyPercent, 0);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(occupancyPercent, 100);

        var entry = Bases.Single(value => value.GetProperty("id").GetString() == baseId);
        if (entry.TryGetProperty("released", out var release) && !release.GetBoolean())
        {
            throw new InvalidOperationException("This generation does not have approved live balance yet.");
        }

        var level = itemLevel ?? entry.GetProperty("item_level").GetInt32();
        ArgumentOutOfRangeException.ThrowIfLessThan(level, entry.GetProperty("item_level").GetInt32());

        var slot = entry.GetProperty("slot").GetString();
        var factory = new ItemFactory(ItemRuleContent.LoadRarityRules(), ItemStatRules.Default);
        var count = Enumerable.Range(0, factory.RarityRules.GetCapacity(rarity))
            .Count(_ => dice.Roll(new(1, 100)).Total <= occupancyPercent);
        var candidates = Affixes
            .Where(family => family.GetProperty("enabled").GetBoolean())
            .Where(family => family.GetProperty("slots").EnumerateArray().Any(value => value.GetString() == slot || slot == "ring" && value.GetString() == "ring1"))
            .Where(family => family.GetProperty("tiers").EnumerateObject().Any(tier => tier.Value.GetProperty("min_ilvl").GetInt32() <= level))
            .ToList();

        if (candidates.Count < count)
        {
            throw new InvalidOperationException("The gated affix pool cannot satisfy this rarity's occupied slots.");
        }

        var affixes = new List<AffixInstance>();

        for (var index = 0; index < count; index++)
        {
            var family = Weighted(candidates, dice);
            candidates.Remove(family);
            var definition = ReadFamily(family);
            var tierRoll = dice.Roll(new(1, 20));
            var eligible = family.GetProperty("tiers").EnumerateObject()
                .Where(tier => tier.Value.GetProperty("min_ilvl").GetInt32() <= level)
                .Select(tier => int.Parse(tier.Name, CultureInfo.InvariantCulture))
                .Order()
                .ToArray();
            var total = tierRoll.Total + (eligible[^1] - 1) / 2 + new GenerationLuck(luck).AffixTierBonus;
            var rank = total <= 8 ? 1 : total <= 13 ? 2 : total <= 16 ? 3 : total == 17 ? 4 : total == 18 ? 5 : total == 19 ? 6 : 7;
            rank = Math.Clamp(rank, eligible[0], eligible[^1]);
            var tierId = $"T{8 - rank}";
            affixes.Add(new AffixRoller().Roll($"{instanceId}:{index}", definition, tierId, dice, luck, tierRoll, (eligible[^1] - 1) / 2));
        }

        return factory.Create(instanceId, ReadBase(entry), rarity, affixes, level);
    }

    private static IReadOnlyList<JsonElement> Load(string name)
    {
        using var document = JsonDocument.Parse(FileAccess.GetFileAsString($"res://Content/Loot/{name}.json"));

        var entries = document.RootElement.EnumerateArray()
            .Select(entry => entry.Clone())
            .ToArray();

        return Array.AsReadOnly(entries);
    }

    private static JsonElement Weighted(IEnumerable<JsonElement> values, IDiceRoller dice)
    {
        var pool = values.ToArray();
        if (pool.Length == 1)
        {
            return pool[0];
        }

        var total = pool.Sum(value => value.GetProperty("weight").GetInt32());
        var roll = dice.Roll(new(1, total)).Total;

        foreach (var value in pool)
        {
            roll -= value.GetProperty("weight").GetInt32();

            if (roll <= 0)
            {
                return value;
            }
        }

        throw new InvalidOperationException("A weighted table needs at least one positive entry.");
    }

    private static ItemDefinition ReadBase(JsonElement entry)
    {
        var size = entry.GetProperty("inventorySize");
        var modifiers = entry.GetProperty("modifiers").EnumerateArray()
            .Select(value => new ItemModifier(new(Enum.Parse<StatType>(value.GetProperty("type").GetString())),
                Enum.Parse<StatOperation>(value.GetProperty("operation").GetString()), value.GetProperty("value").GetDecimal()));
        WeaponProperties weapon = null;

        if (entry.TryGetProperty("weapon", out var data))
        {
            weapon = new(data.GetProperty("attacksPerSecond").GetDecimal(), data.GetProperty("manaCost").GetDecimal(),
                data.GetProperty("damage").EnumerateArray()
                    .Select(part => new DamageComponent(new(part.GetProperty("count").GetInt32(), part.GetProperty("sides").GetInt32()),
                        part.GetProperty("flat").GetInt32(), Enum.Parse<DamageType>(part.GetProperty("type").GetString()))));
        }

        return new(entry.GetProperty("id").GetString(), entry.GetProperty("name").GetString(),
            entry.GetProperty("tags").EnumerateArray().Select(tag => tag.GetString()), modifiers, weapon,
            new(size.GetProperty("width").GetInt32(), size.GetProperty("height").GetInt32()),
            entry.TryGetProperty("required_level", out var required) ? new EquipmentRequirement(required.GetInt32(),
                Enum.Parse<StatType>(entry.GetProperty("required_attribute").GetString()),
                entry.GetProperty("required_attribute_value").ValueKind == JsonValueKind.Null ? 0 : entry.GetProperty("required_attribute_value").GetInt32(),
                entry.GetProperty("released").GetBoolean()) : null);
    }

    private static AffixDefinition ReadFamily(JsonElement family) => new(family.GetProperty("id").GetString(),
        family.GetProperty("id").GetString(), family.GetProperty("weight").GetInt32(),
        family.GetProperty("tiers").EnumerateObject()
            .Select(tier => new AffixTier(tier.Value.GetProperty("tier_id").GetString(), 0, ReadRanges(family, tier.Value), tier.Value.GetProperty("min_ilvl").GetInt32())));

    internal static IEnumerable<AffixModifierRange> ReadRanges(JsonElement family, JsonElement tier)
    {
        var effect = family.GetProperty("effect").GetString();
        var id = family.GetProperty("id").GetString();
        var types = family.TryGetProperty("types", out var kinds)
            ? kinds.EnumerateArray()
                .Select(kind => (DamageType?)Enum.Parse<DamageType>(kind.GetString()))
                .ToArray()
            : new DamageType?[] { null };
        var type = effect switch
        {
            "hp" or "hp_percent" => StatType.MaxHealth,
            "mana" or "mana_percent" => StatType.MaxMana,
            "armor" or "armor_percent" => StatType.Armor,
            "evasion" or "evasion_percent" => StatType.Evasion,
            "movement" => StatType.MovementSpeed,
            "attack_speed" => StatType.AttackSpeed,
            "added" => StatType.AddedDamage,
            "increased" => StatType.DamageMultiplier,
            "resistance" => StatType.Resistance,
            "hp_regen" => StatType.HealthRecovery,
            "mana_regen" => StatType.ManaRecovery,
            "accuracy" => StatType.AttackModifier,
            "attribute" when id == "all-attributes" => StatType.Strength,
            "attribute" => Enum.Parse<StatType>(id, true),
            "dice" => Enum.Parse<StatType>($"AddedDamageD{tier.GetProperty("maximum").GetInt32()}"),
            _ => throw new InvalidOperationException($"Unsupported live affix effect: {effect}")
        };
        var operation = effect.EndsWith("_percent", StringComparison.Ordinal) || effect is "movement" or "attack_speed" or "increased"
            ? StatOperation.Increased : StatOperation.Flat;
        var minimum = tier.GetProperty("minimum").GetDecimal();
        var maximum = effect == "dice" ? minimum : tier.GetProperty("maximum").GetDecimal();
        var precision = family.GetProperty("precision").GetInt32();

        if (id == "all-attributes")
        {
            return new[] { StatType.Strength, StatType.Dexterity, StatType.Intelligence, StatType.Luck }
                .Select(attribute => new AffixModifierRange(new(attribute), operation, minimum, maximum, precision));
        }

        return types.Select(kind => new AffixModifierRange(new(type, kind), operation, minimum, maximum, precision));
    }
}

/// <summary>Retains independent group results and the optional generated item without crediting a wallet.</summary>
/// <param name="Gold">Captured guaranteed gold and its natural/effective amount rolls.</param>
/// <param name="Equipment">Captured equipment occurrence and rarity result.</param>
/// <param name="Item">The generated item, absent when the equipment group selected nothing.</param>
public sealed record MonsterLoot(LootGroupRoll Gold, LootGroupRoll Equipment, ItemInstance Item);
