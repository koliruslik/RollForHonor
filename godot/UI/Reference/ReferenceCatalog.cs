using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Godot;
using RollForHonor.Domain.Combat.Damage.Models;
using RollForHonor.Domain.Items.Models;
using RollForHonor.Domain.Stats;
using RollForHonor.Godot.Content.Items;
using RollForHonor.Godot.UI.Common;
using RollForHonor.Godot.UI.Inventory;

namespace RollForHonor.Godot.UI.Reference;

/// <summary>Indexes shared rule explanations, authored help and immutable session inspection snapshots.</summary>
public static class ReferenceCatalog
{
    /// <summary>Builds an independent reference snapshot without creating items or rolling dice.</summary>
    /// <param name="items">Optional currently owned items; absent omits session-specific item entries.</param>
    /// <param name="rolls">Optional captured recent-roll history.</param>
    /// <param name="combat">Optional captured combat journal.</param>
    /// <returns>Stable, unique entries sorted by category and title.</returns>
    /// <exception cref="JsonException">Authored reference content is malformed.</exception>
    /// <exception cref="ArgumentException">A content or item identity is duplicated.</exception>
    /// <exception cref="InvalidOperationException">An authored topic links to a missing entry.</exception>
    public static IReadOnlyList<ReferenceEntry> Create(IEnumerable<ItemInstance> items = null, InspectionTopic rolls = null, InspectionTopic combat = null)
    {
        var entries = new Dictionary<string, ReferenceEntry>(StringComparer.Ordinal);
        AddRules(entries);
        AddAuthoredTopics(entries);
        AddItems(entries, items ?? []);

        if (rolls is not null)
        {
            entries.Add("session.rolls", new("session.rolls", "Current session", rolls));
        }

        if (combat is not null)
        {
            entries.Add("session.combat", new("session.combat", "Current session", combat));
        }

        return entries.Values
            .OrderBy(entry => entry.Category, StringComparer.Ordinal)
            .ThenBy(entry => entry.Topic.Title, StringComparer.Ordinal)
            .ToArray();
    }

    private static void AddRules(IDictionary<string, ReferenceEntry> entries)
    {
        var scalarStats = Enum.GetValues<StatType>()
            .Where(type => type is not (StatType.Resistance or StatType.AddedDamage));

        foreach (var type in scalarStats)
        {
            var id = $"stat.{type}";
            entries.Add(id, new(id, "Character stats", CombatGlossary.Stat(new(type))));
        }

        entries.Add("damage.physical", new("damage.physical", "Damage", CombatGlossary.Physical()));

        foreach (var type in Enum.GetValues<DamageType>())
        {
            entries.Add($"damage.{type}", new($"damage.{type}", "Damage", CombatGlossary.Damage(type)));
            entries.Add($"resistance.{type}", new($"resistance.{type}", "Resistances", CombatGlossary.Resistance(type)));
            entries.Add($"added.{type}", new($"added.{type}", "Damage", CombatGlossary.Stat(new(StatType.AddedDamage, type))));
        }

        var rarities = ItemRuleContent.LoadRarityRules();

        foreach (var rarity in Enum.GetValues<ItemRarity>())
        {
            var topic = new InspectionTopic(rarity.ToString(), $"Configured affix limit: {rarities.GetCapacity(rarity)}.\n\nRarity is explicit. Adding or removing affixes does not change it automatically.", ItemPresentation.RarityColor(rarity));
            entries.Add($"rarity.{rarity}", new($"rarity.{rarity}", "Items", topic));
        }
    }

    private static void AddAuthoredTopics(IDictionary<string, ReferenceEntry> entries)
    {
        using var json = JsonDocument.Parse(FileAccess.GetFileAsString("res://Content/Reference/topics.json"));
        var authored = json.RootElement.EnumerateArray()
            .Select(entry => new AuthoredTopic(entry.GetProperty("id").GetString(), entry.GetProperty("category").GetString(),
                entry.GetProperty("title").GetString(), entry.GetProperty("description").GetString(), entry.GetProperty("related").EnumerateArray()
                    .Select(link => link.GetString())
                    .ToArray()))
            .ToArray();

        foreach (var source in authored)
        {
            var links = source.Related
                .Select(id => new InspectionLink(id, HonorTheme.Secondary, () => entries[id].Topic));
            entries.Add(source.Id, new(source.Id, source.Category, new(source.Title, source.Description, HonorTheme.Secondary, links)));
        }

        foreach (var source in authored)
        {
            if (source.Related.Any(id => !entries.ContainsKey(id)))
            {
                throw new InvalidOperationException($"Reference topic '{source.Id}' contains a missing link.");
            }

            var links = source.Related
                .Select(id => new InspectionLink(entries[id].Topic.Title, entries[id].Topic.Accent, () => entries[id].Topic));
            entries[source.Id] = new(source.Id, source.Category, new(source.Title, source.Description, HonorTheme.Secondary, links));
        }
    }

    private static void AddItems(IDictionary<string, ReferenceEntry> entries, IEnumerable<ItemInstance> items)
    {
        foreach (var item in items)
        {
            entries.Add($"item.{item.Id}", new($"item.{item.Id}", "Owned items", ItemInspectionTopics.Item(item)));

            foreach (var affix in item.Affixes)
            {
                var topic = ItemInspectionTopics.Affix(item.Rarity, affix, true);
                var title = item.Base.Name + " · " + topic.Title;
                var id = $"affix.{item.Id.Length}:{item.Id}.{affix.Id}";
                entries.Add(id, new(id, "Rolled affixes", new(title, topic.Description, topic.Accent, topic.Links, topic.Facts)));
            }
        }
    }

    private sealed record AuthoredTopic(string Id, string Category, string Title, string Description, string[] Related);
}
