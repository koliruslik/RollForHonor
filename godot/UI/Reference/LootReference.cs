using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using Godot;
using RollForHonor.Domain.Combat.Damage.Models;
using RollForHonor.Domain.Items.Models;
using RollForHonor.Domain.Stats;
using RollForHonor.Godot.Content.Items;
using RollForHonor.Godot.Content.Loot;
using RollForHonor.Godot.Gameplay.Stats;
using RollForHonor.Godot.UI.Common;
using RollForHonor.Godot.UI.Inventory;
using RollForHonor.Godot.UI.Localization;

namespace RollForHonor.Godot.UI.Reference;

/// <summary>Builds the bestiary, base table and single affix table from live game content.</summary>
public static class LootReference
{
    /// <summary>Captures catalog pages without creating actors, owned items or random rolls.</summary>
    /// <param name="luck">Captured player Luck, or zero for the title-screen baseline.</param>
    /// <returns>One page per monster and one shared table each for bases and affixes.</returns>
    public static IReadOnlyList<ReferenceEntry> Create(decimal luck = 0)
    {
        var result = GameLootCatalog.Monsters
            .Select(monster => Monster(monster, Math.Max(0, luck)))
            .ToList();
        result.Add(new("loot.monster.training", L.Text("Monsters"), new(L.Text("Training target"),
            L.Text("A stationary practice target. It does not attack and drops no gold or equipment."), HonorTheme.Muted)));
        result.Add(new("loot.bases", L.Text("Items"), new(L.Text("Items"), "", HonorTheme.Parchment), Bases()));
        result.Add(new("loot.affixes", L.Text("Affixes"), new(L.Text("Affixes"), "", HonorTheme.Secondary), Affixes()));

        return result.AsReadOnly();
    }

    private static ReferenceEntry Monster(JsonElement monster, decimal luck)
    {
        using var definition = ResourceLoader.Load<ActorStatDefinition>(monster.GetProperty("stats_resource").GetString(), cacheMode: ResourceLoader.CacheMode.Ignore);
        var stats = new StatCalculator().Calculate(definition.CreateProfile());
        var description = L.Format($"Health {stats.Get(new(StatType.MaxHealth)):0} · Armor {stats.Get(new(StatType.Armor)):0} · Evasion {stats.Get(new(StatType.Evasion)):0} · Speed {stats.Get(new(StatType.MovementSpeed)):0.##}\nItem level {monster.GetProperty("item_level").GetInt32()}");
        var attack = SentinelAttackContent.Create();
        var attackText = L.Format($"Arrow: {L.Damage(attack.Attack.Damage[0])} · Attack roll +{attack.Attack.AttackModifier} · Every {attack.Cooldown.TotalSeconds:0.##} s");
        var chances = EquipmentChances(monster, luck);
        var chance = 100m * (1 - chances.GetValueOrDefault("none"));
        var behavior = L.Format($"Approaches until in range, then fires arrows. Gold always drops; equipment has a {chance:0.##}% chance. Table chances are per kill.");

        var topic = new InspectionTopic(L.Text(monster.GetProperty("name_en").GetString()), L.Format($"{description}\n{attackText}\n\n{behavior}"), HonorTheme.Brass,
            facts: [new(L.Format($"Luck {luck:0.##} · Equipment {chance:0.##}%"), HonorTheme.Brass)]);

        return new($"loot.monster.{monster.GetProperty("id").GetString()}", L.Text("Monsters"), topic, MonsterLoot(monster, luck), luck);
    }

    private static Dictionary<string, decimal> EquipmentChances(JsonElement monster, decimal luck)
    {
        var group = GameLootCatalog.EquipmentGroup(monster.GetProperty("id").GetString(), luck);
        return group.Bands.Select((band, index) => new
        {
            Name = band.Outcome.IsEmpty ? "none" : band.Outcome.Rarity.Value.ToString(),
            Faces = (index + 1 < group.Bands.Count ? group.Bands[index + 1].MinimumRoll : group.Dice.Sides + 1) - band.MinimumRoll
        })
            .ToDictionary(value => value.Name, value => (decimal)value.Faces / group.Dice.Sides, StringComparer.Ordinal);
    }

    private static ReferenceTable MonsterLoot(JsonElement monster, decimal luck)
    {
        var gold = monster.GetProperty("gold");
        var rows = new List<ReferenceTableRow>
        {
            new("gold", [L.Text("Gold"), "—", "100%", $"{gold.GetProperty("minimum").GetInt32()}–{gold.GetProperty("maximum").GetInt32()}", "—"], HonorTheme.Brass, "gold")
        };
        var outcomes = monster.GetProperty("equipment_outcomes").EnumerateArray().ToArray();
        var chances = EquipmentChances(monster, luck);
        var capacities = ItemRuleContent.LoadRarityRules();
        var filters = new List<string> { "gold" };

        foreach (var outcome in outcomes)
        {
            var name = outcome.GetProperty("rarity").GetString();
            var chance = chances.GetValueOrDefault(name);

            if (name == "none")
            {
                continue;
            }

            var rarity = Enum.Parse<ItemRarity>(name);
            var capacity = capacities.GetCapacity(rarity);
            var affixCount = capacity == 0 ? "0" : $"0–{capacity}";
            filters.Add(name);

            foreach (var slot in monster.GetProperty("slots").EnumerateArray())
            {
                var slotId = slot.GetProperty("slot").GetString();
                var bases = GameLootCatalog.Bases
                    .Where(value => value.GetProperty("loot_enabled").GetBoolean())
                    .Where(value => value.GetProperty("slot").GetString() == slotId)
                    .Where(value => value.GetProperty("item_level").GetInt32() <= monster.GetProperty("item_level").GetInt32())
                    .ToArray();
                var baseWeight = bases.Sum(value => value.GetProperty("weight").GetInt32());
                var slotChance = (decimal)slot.GetProperty("weight").GetInt32() / monster.GetProperty("slots").EnumerateArray().Sum(value => value.GetProperty("weight").GetInt32());

                foreach (var entry in bases)
                {
                    var itemChance = chance * slotChance * entry.GetProperty("weight").GetInt32() / baseWeight;
                    var id = entry.GetProperty("id").GetString();
                    rows.Add(new($"{name}.{id}", [ItemPresentation.Name(entry.GetProperty("id").GetString(), entry.GetProperty("name").GetString(), rarity), L.Text(name), Percent(itemChance), "1", affixCount],
                        ItemPresentation.RarityColor(rarity), name + "|" + slotId, entry.GetProperty("name").GetString() + " " + entry.GetProperty("name_uk").GetString(), BaseTopic(entry)));
                }
            }
        }

        return new([new("Drop", 235), new("Rarity", 110), new("Per kill", 100), new("Amount", 80), new("Affixes", 80)], rows, filters);
    }

    private static ReferenceTable Bases()
    {
        var rows = GameLootCatalog.Bases
            .Where(entry => entry.GetProperty("catalog_visible").GetBoolean())
            .SelectMany(entry => Enum.GetValues<ItemRarity>()
                .Select(rarity => (Entry: entry, Rarity: rarity)))
            .OrderBy(value => value.Entry.GetProperty("generation").GetInt32())
            .ThenBy(value => value.Rarity)
            .ThenBy(value => value.Entry.GetProperty("branch").GetString(), StringComparer.Ordinal)
            .Select(value =>
            {
                var entry = value.Entry;
                var rarity = value.Rarity;
                var released = entry.GetProperty("released").GetBoolean();
                var requirements = RequirementsTopic(entry);
                var properties = PropertiesTopic(entry);
                return new ReferenceTableRow($"{entry.GetProperty("id").GetString()}.{rarity}",
                    [ItemPresentation.Name(entry.GetProperty("id").GetString(), entry.GetProperty("name").GetString(), rarity),
                     released ? entry.GetProperty("item_level").GetInt32().ToString(CultureInfo.InvariantCulture) : "—",
                     released ? Requirements(entry) : "—",
                     released ? BaseProperties(entry) : "—"],
                    ItemPresentation.RarityColor(rarity), entry.GetProperty("slot").GetString() + "|" + rarity + "|" + entry.GetProperty("branch").GetString(),
                    entry.GetProperty("name").GetString() + " " + entry.GetProperty("name_uk").GetString(), BaseTopic(entry),
                    [BaseTopic(entry), ItemLevelTopic(), requirements, properties]);
            });

        return new([new("Item", 300), new("ilvl", 100, ItemLevelTopic()),
            new("Requirements", 210, new(L.Text("Requirements"), L.Text("Equipment checks your character level and allocated Strength, Dexterity or Intelligence. Item bonuses cannot satisfy their own requirements."), HonorTheme.Brass,
                [CombatGlossary.Link(new(StatType.CharacterLevel)), CombatGlossary.Link(new(StatType.Strength)), CombatGlossary.Link(new(StatType.Dexterity)), CombatGlossary.Link(new(StatType.Intelligence))])),
            new("Properties", 334, new(L.Text("Properties"), L.Text("These are the item's base values, before rolled affixes. Select a property cell to inspect damage, defenses, speed and mana cost."), HonorTheme.Secondary))], rows,
            ["Common", "Uncommon", "Rare", "Epic", "Legendary", "weapon", "body", "helmet", "boots", "offhand", "amulet", "ring"]);
    }

    private static ReferenceTable Affixes()
    {
        var tiers = Enumerable.Range(1, 7)
            .Select(AffixTierTable)
            .ToArray();
        return new(tiers[0].Columns, tiers[0].Rows, tiers[0].Filters, tiers);
    }

    private static ReferenceTable AffixTierTable(int rank)
    {
        var rows = GameLootCatalog.Affixes.Select(family =>
        {
            var slots = family.GetProperty("slots").EnumerateArray()
                .Select(slot => slot.GetString() is "ring1" or "ring2" ? "ring" : slot.GetString())
                .Distinct(StringComparer.Ordinal)
                .ToArray();
            var exists = family.GetProperty("tiers").TryGetProperty(rank.ToString(CultureInfo.InvariantCulture), out var tier);
            var links = exists && family.GetProperty("enabled").GetBoolean()
                ? GameLootCatalog.ReadRanges(family, tier)
                    .Select(range => CombatGlossary.Link(range.Key))
                    .ToArray() : [];
            var topic = new InspectionTopic(L.Text(family.GetProperty("name_en").GetString()),
                exists ? L.Format($"T{8 - rank} · {Range(family, tier)}\nItem level {tier.GetProperty("min_ilvl").GetInt32()}")
                    : L.Text("This family has no value range for the selected tier."), HonorTheme.Parchment, links);
            var slotTopic = new InspectionTopic(L.Text("Slots"), string.Join(", ", slots.Select(Slot)), HonorTheme.Secondary);
            return new ReferenceTableRow(family.GetProperty("id").GetString(),
                [L.Text(family.GetProperty("name_en").GetString()), SlotSummary(slots), exists ? Range(family, tier) : "—",
                 exists ? tier.GetProperty("min_ilvl").GetInt32().ToString(CultureInfo.InvariantCulture) : "—"], HonorTheme.Parchment,
                string.Join('|', slots), family.GetProperty("name_en").GetString() + " " + family.GetProperty("name_uk").GetString(), topic,
                [topic, slotTopic, topic, ItemLevelTopic()]);
        });

        return new([new("Affix", 300), new("Slots", 280), new("Value range", 230), new("ilvl", 130, ItemLevelTopic())], rows,
            ["weapon", "body", "helmet", "boots", "offhand", "amulet", "ring"]);
    }

    private static InspectionTopic ItemLevelTopic() => new(L.Text("ilvl"),
        L.Text("Item level determines which affix tiers an item can roll. It is separate from the character level required to equip it. T7 is the weakest tier and T1 is the strongest."), HonorTheme.Brass,
        [new(L.Text("Level"), HonorTheme.Brass, () => CombatGlossary.Stat(new(StatType.CharacterLevel)))]);

    private static string Requirements(JsonElement entry) => L.Format($"Lv {entry.GetProperty("required_level").GetInt32()} · {L.Text(entry.GetProperty("branch").GetString())} {entry.GetProperty("required_attribute_value").GetInt32()}");

    private static InspectionTopic RequirementsTopic(JsonElement entry) => new(L.Text("Requirements"),
        entry.GetProperty("released").GetBoolean() ? Requirements(entry) : L.Text("Requirements have not been approved yet."), HonorTheme.Brass,
        [CombatGlossary.Link(new(StatType.CharacterLevel)), CombatGlossary.Link(new(Enum.Parse<StatType>(entry.GetProperty("branch").GetString())))]);

    private static InspectionTopic PropertiesTopic(JsonElement entry)
    {
        var links = new List<InspectionLink>();
        var facts = new List<InspectionFact>();

        void Add(string text, InspectionTopic explanation)
        {
            facts.Add(new(text, explanation.Accent, LinkIndex: links.Count));
            links.Add(new(explanation.Title, explanation.Accent, () => explanation));
        }

        if (!entry.GetProperty("released").GetBoolean())
        {
            return new(L.Text("Properties"), L.Text("Balance pending"), HonorTheme.Secondary);
        }

        foreach (var value in entry.GetProperty("modifiers").EnumerateArray())
        {
            var key = new StatKey(Enum.Parse<StatType>(value.GetProperty("type").GetString()));
            var modifier = new ItemModifier(key, Enum.Parse<StatOperation>(value.GetProperty("operation").GetString()), value.GetProperty("value").GetDecimal());
            Add(ItemPresentation.Modifier(modifier), CombatGlossary.Stat(key));
        }

        if (entry.TryGetProperty("weapon", out var weapon))
        {
            foreach (var part in weapon.GetProperty("damage").EnumerateArray())
            {
                var type = Enum.Parse<DamageType>(part.GetProperty("type").GetString());
                Add(L.Damage(new(new(part.GetProperty("count").GetInt32(), part.GetProperty("sides").GetInt32()),
                    part.GetProperty("flat").GetInt32(), type)), CombatGlossary.Damage(type));
            }

            Add(L.Format($"{weapon.GetProperty("attacksPerSecond").GetDecimal():0.##} atk/s · {weapon.GetProperty("manaCost").GetDecimal() * weapon.GetProperty("attacksPerSecond").GetDecimal():0.##} mana/s"),
                CombatGlossary.Stat(new(StatType.AttackSpeed)));
            Add(L.Format($"{L.Text("Mana cost")}: {weapon.GetProperty("manaCost").GetDecimal():0.##}"),
                new(L.Text("Mana cost"), L.Format($"{weapon.GetProperty("manaCost").GetDecimal():0.##} mana per attack. The displayed mana per second assumes continuous attacks at the base attack rate."),
                    HonorTheme.Secondary, [CombatGlossary.Link(new(StatType.MaxMana)), CombatGlossary.Link(new(StatType.ManaRecovery))]));
        }

        return new(L.Text("Properties"), facts.Count == 0 ? "—" : "", HonorTheme.Secondary, links, facts);
    }

    private static InspectionTopic BaseTopic(JsonElement entry)
    {
        var properties = PropertiesTopic(entry);
        var level = entry.GetProperty("released").GetBoolean() ? entry.GetProperty("item_level").GetInt32().ToString(CultureInfo.InvariantCulture) : "—";
        var links = new List<InspectionLink>
        {
            new(L.Text("ilvl"), HonorTheme.Brass, ItemLevelTopic),
            new(L.Text("Requirements"), HonorTheme.Brass, () => RequirementsTopic(entry))
        };
        var facts = new List<InspectionFact>
        {
            new($"ilvl {level}", HonorTheme.Brass, LinkIndex: 0),
            new(entry.GetProperty("released").GetBoolean() ? Requirements(entry) : L.Text("Requirements have not been approved yet."), HonorTheme.Brass, LinkIndex: 1)
        };
        links.AddRange(properties.Links);
        facts.AddRange(properties.Facts.Select(fact => fact with { LinkIndex = fact.LinkIndex + 2 }));
        return new(L.Text(entry.GetProperty("name").GetString()), properties.Description, HonorTheme.Parchment, links, facts);
    }

    private static string BaseProperties(JsonElement entry)
    {
        var values = entry.GetProperty("modifiers").EnumerateArray()
            .Select(value => ItemPresentation.Modifier(new(new(Enum.Parse<StatType>(value.GetProperty("type").GetString())),
                Enum.Parse<StatOperation>(value.GetProperty("operation").GetString()), value.GetProperty("value").GetDecimal())))
            .ToList();

        if (entry.TryGetProperty("weapon", out var weapon))
        {
            values.AddRange(weapon.GetProperty("damage").EnumerateArray()
                .Select(part => L.Damage(new(new(part.GetProperty("count").GetInt32(), part.GetProperty("sides").GetInt32()),
                    part.GetProperty("flat").GetInt32(), Enum.Parse<DamageType>(part.GetProperty("type").GetString())))));
            values.Add(L.Format($"{weapon.GetProperty("attacksPerSecond").GetDecimal():0.##} atk/s · {weapon.GetProperty("manaCost").GetDecimal() * weapon.GetProperty("attacksPerSecond").GetDecimal():0.##} mana/s"));
        }

        return values.Count == 0 ? "—" : string.Join("\n", values);
    }

    private static string SlotSummary(string[] slots)
    {
        if (slots.Length == 7)
        {
            return L.Text("All equipment");
        }

        if (slots.Length == 6 && !slots.Contains("weapon", StringComparer.Ordinal))
        {
            return L.Text("Armor, offhand, jewelry");
        }

        if (slots.Length == 4 && slots.Contains("weapon", StringComparer.Ordinal))
        {
            return L.Text("Weapon, offhand, jewelry");
        }

        if (slots.Length == 4 && slots.Contains("body", StringComparer.Ordinal))
        {
            return L.Text("Armor and offhand");
        }

        return string.Join(", ", slots.Select(Slot));
    }

    private static string Slot(string slot) => L.Text(slot switch
    {
        "weapon" => "Main hand",
        "body" => "Body armor",
        "helmet" => "Helmet",
        "boots" => "Boots",
        "offhand" => "Off hand",
        "amulet" => "Amulet",
        "ring" => "Ring",
        _ => slot
    });

    private static string Percent(decimal value) => (value * 100).ToString("0.####", CultureInfo.InvariantCulture) + "%";

    private static string Range(JsonElement family, JsonElement tier)
    {
        if (family.GetProperty("effect").GetString() == "dice")
        {
            return $"{tier.GetProperty("minimum").GetInt32()}d{tier.GetProperty("maximum").GetInt32()}";
        }

        var unit = family.GetProperty("unit").GetString();
        var scale = unit == "fraction" ? 100m : 1m;
        var minimum = (tier.GetProperty("minimum").GetDecimal() * scale).ToString("0.##", CultureInfo.InvariantCulture);
        var maximum = (tier.GetProperty("maximum").GetDecimal() * scale).ToString("0.##", CultureInfo.InvariantCulture);
        var suffix = unit == "fraction" ? "%" : unit == "per_second" ? L.Text("/s") : unit == "multiplier" ? "×" : "";

        return $"{minimum}–{maximum}{suffix}";
    }
}
