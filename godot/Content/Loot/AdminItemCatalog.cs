using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using RollForHonor.Domain.Dice.Models;
using RollForHonor.Domain.Dice.Services;
using RollForHonor.Domain.Combat.Effects.Models;
using RollForHonor.Domain.Items.Affixes.Models;
using RollForHonor.Domain.Items.Affixes.Services;
using RollForHonor.Domain.Items.Configuration;
using RollForHonor.Domain.Items.Models;
using RollForHonor.Domain.Items.Services;
using RollForHonor.Godot.Content.Items;
using RollForHonor.Domain.Stats;

namespace RollForHonor.Godot.Content.Loot;

internal sealed record AdminAffixChoice(string DefinitionId, string TierId, decimal Quality, AffixInstance Original = null);

internal static class AdminItemCatalog
{
    internal static ItemInstance Empty(string baseId, string id, ItemRarity rarity, int level)
    {
        var entry = GameLootCatalog.Bases.Single(value => value.GetProperty("id").GetString() == baseId);

        if (!entry.GetProperty("released").GetBoolean() || level < entry.GetProperty("item_level").GetInt32())
        {
            throw new ArgumentException("The selected base or item level is not available.", nameof(baseId));
        }

        return Factory().Create(id, GameLootCatalog.ReadBase(entry), rarity, generationLevel: level);
    }

    internal static JsonElement[] Families(ItemDefinition definition, int level)
    {
        var entry = GameLootCatalog.Bases.FirstOrDefault(value => value.GetProperty("id").GetString() == definition.Id);

        if (entry.ValueKind == JsonValueKind.Undefined)
        {
            return [];
        }

        var slot = entry.GetProperty("slot").GetString();
        return GameLootCatalog.Affixes
            .Where(family => family.GetProperty("enabled").GetBoolean())
            .Where(family => family.GetProperty("slots")
                .EnumerateArray()
                .Any(value => value.GetString() == slot || slot == "ring" && value.GetString() is "ring1" or "ring2"))
            .Where(family => Tiers(family, level).Length > 0)
            .OrderBy(family => family.GetProperty("name_en").GetString(), StringComparer.Ordinal)
            .ToArray();
    }

    internal static JsonElement[] Tiers(JsonElement family, int level)
    {
        var status = IsStatus(family);
        var tiers = family.GetProperty("tiers")
            .EnumerateObject()
            .Select(tier => tier.Value)
            .Where(tier => status || tier.GetProperty("min_ilvl").GetInt32() <= level)
            .OrderBy(tier => int.Parse(tier.GetProperty("tier_id").GetString().AsSpan(1)))
            .ToArray();
        return status ? tiers
            .Append(JsonSerializer.SerializeToElement(new { tier_id = "T0" }))
            .ToArray() : tiers;
    }

    internal static ItemInstance Build(ItemInstance source, ItemRarity rarity, int level, IReadOnlyList<AdminAffixChoice> choices)
    {
        var families = Families(source.Base, level);
        var affixes = choices
            .Select(choice =>
            {
                var family = families.FirstOrDefault(value => value.GetProperty("id").GetString() == choice.DefinitionId);

                if (family.ValueKind == JsonValueKind.Undefined || !Tiers(family, level)
                    .Any(tier => tier.GetProperty("tier_id").GetString() == choice.TierId) || choice.Quality is < 1 or > 100)
                {
                    throw new ArgumentException("An affix, tier or quality is unavailable for this item.", nameof(choices));
                }

                if (choice.Original is { } existing && source.Affixes.Contains(existing)
                    && existing.DefinitionId == choice.DefinitionId && existing.TierId == choice.TierId
                    && existing.EffectiveQualityRoll == choice.Quality)
                {
                    return existing;
                }

                return new AffixRoller().Roll(Guid.NewGuid().ToString("N"), Definition(family),
                    choice.TierId, new QualityDice(checked((int)decimal.Round(choice.Quality, 0, MidpointRounding.AwayFromZero))));
            })
            .ToArray();

        return Factory().Create(source.Id, source.Base, rarity, affixes, level);
    }

    private static ItemFactory Factory() => new(ItemRuleContent.LoadRarityRules(), ItemStatRules.Default);

    private static bool IsStatus(JsonElement family) => family.GetProperty("effect").GetString() is "poison" or "ignite" or "bleed";

    private static AffixDefinition Definition(JsonElement family)
    {
        var standard = GameLootCatalog.ReadFamily(family);

        if (!IsStatus(family))
        {
            return standard;
        }

        var keys = StatusStatKeys.For(Enum.Parse<StatusEffectKind>(family.GetProperty("effect").GetString(), true));
        var strongest = standard.Tiers.Single(tier => tier.Id == "T1");
        var testTier = new AffixTier("T0", 0, strongest.Modifiers
            .Select(range => range.Key.Type == keys.Chance
                ? new AffixModifierRange(new(keys.Guaranteed), StatOperation.Flat, 1, 1, 0) : range));
        return new(standard.Id, standard.FamilyId, standard.SelectionWeight,
            standard.Tiers.Append(testTier), standard.RequiredItemTags);
    }

    private sealed class QualityDice(int quality) : IDiceRoller
    {
        private readonly List<DiceRoll> _rolls = [];

        /// <inheritdoc />
        public DiceTrace Trace => new(_rolls);

        /// <inheritdoc />
        public DiceRoll Roll(DiceFormula formula)
        {
            if (formula.Count != 1 || formula.Sides != 100 || _rolls.Count != 0)
            {
                throw new InvalidOperationException("An administrative affix selection supplies one quality roll.");
            }

            var roll = new DiceRoll(formula, [quality], quality);
            _rolls.Add(roll);
            return roll;
        }
    }
}
