using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Godot;
using RollForHonor.Application.Items.Upgrades;
using RollForHonor.Domain.Items.Affixes.Models;
using RollForHonor.Domain.Items.Configuration;
using RollForHonor.Domain.Items.Models;
using RollForHonor.Godot.Content.Loot;

namespace RollForHonor.Godot.Content.Items;

/// <summary>Loads paid-operation rules and adapts live enabled affixes to the shared Luck generator.</summary>
public sealed class ItemUpgradeContent : IItemUpgradeAffixSource
{
    /// <summary>Gets the immutable resource and progression rules loaded from runtime content.</summary>
    public static ItemUpgradeRules Rules { get; } = LoadRules();

    /// <inheritdoc />
    public IReadOnlyList<AffixDefinition> GetAffixes(ItemInstance item)
    {
        var entry = GameLootCatalog.Bases.FirstOrDefault(value => value.GetProperty("id").GetString() == item.Base.Id);
        if (entry.ValueKind == JsonValueKind.Undefined)
        {
            return [];
        }

        var slot = entry.GetProperty("slot").GetString();
        return GameLootCatalog.Affixes
            .Where(family => family.GetProperty("enabled").GetBoolean())
            .Where(family => family.GetProperty("slots").EnumerateArray()
                .Any(value => value.GetString() == slot || slot == "ring" && value.GetString() is "ring1" or "ring2"))
            .Where(family => family.GetProperty("tiers").EnumerateObject()
                .Any(tier => tier.Value.GetProperty("min_ilvl").GetInt32() <= item.GenerationLevel))
            .Select(family => GatedFamily(family, item.GenerationLevel))
            .ToArray();
    }

    private static AffixDefinition GatedFamily(JsonElement family, int level)
    {
        var ranks = family.GetProperty("tiers").EnumerateObject()
            .Where(tier => tier.Value.GetProperty("min_ilvl").GetInt32() <= level)
            .Select(tier => int.Parse(tier.Name, System.Globalization.CultureInfo.InvariantCulture))
            .Order()
            .ToArray();
        var shift = (ranks[^1] - 1) / 2;
        int[] thresholds = [0, 1, 9, 14, 17, 18, 19, 20];
        return new(family.GetProperty("id").GetString(), family.GetProperty("id").GetString(),
            family.GetProperty("weight").GetInt32(), ranks.Select(rank =>
            {
                var tier = family.GetProperty("tiers").GetProperty(rank.ToString(System.Globalization.CultureInfo.InvariantCulture));
                return new AffixTier(tier.GetProperty("tier_id").GetString(),
                    rank == ranks[0] ? 1 : Math.Max(1, thresholds[rank] - shift), GameLootCatalog.ReadRanges(family, tier),
                    tier.GetProperty("min_ilvl").GetInt32());
            }));
    }

    private static ItemUpgradeRules LoadRules()
    {
        using var document = JsonDocument.Parse(FileAccess.GetFileAsString("res://Content/Items/upgrade-rules.json"));
        var data = document.RootElement;
        return new(data.GetProperty("enhancement_prices").EnumerateArray().Select(value => value.GetInt64()),
            data.GetProperty("evolution_gates").EnumerateArray().Select(value => value.GetInt32()),
            data.GetProperty("evolution_prices").EnumerateArray().Select(value => value.GetInt64()),
            data.GetProperty("resource").GetString(), data.GetProperty("enhancement_step").GetDecimal(),
            data.GetProperty("reroll_base").GetInt64(), data.GetProperty("reroll_increment").GetInt64(),
            data.GetProperty("lock_multiplier").GetDecimal(), data.GetProperty("addition_multiplier").GetDecimal(),
            data.GetProperty("maximum_base_level").GetInt32());
    }
}
