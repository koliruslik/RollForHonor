using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Godot;
using RollForHonor.Domain.Combat.Damage.Models;
using RollForHonor.Domain.Combat.Effects.Models;

namespace RollForHonor.Godot.Content.Combat;

/// <summary>Loads production status balance separately from calculation code and source talent/buff inputs.</summary>
public static class StatusEffectContent
{
    /// <summary>Gets validated poison, ignite and bleed definitions from editable content.</summary>
    public static IReadOnlyList<OnHitStatusRule> Rules { get; } = Load();

    private static IReadOnlyList<OnHitStatusRule> Load()
    {
        using var document = JsonDocument.Parse(FileAccess.GetFileAsString("res://Content/Combat/status-effects.json"));
        var root = document.RootElement;
        var reference = root.GetProperty("reference_attacks_per_second").GetDecimal();
        var sides = root.GetProperty("chance_roll_sides").GetInt32();
        var rules = root.GetProperty("effects").EnumerateArray()
            .Select(entry => new OnHitStatusRule(Enum.Parse<StatusEffectKind>(entry.GetProperty("kind").GetString()),
                new(Guid.Parse(entry.GetProperty("id").GetString())), entry.GetProperty("dice_sides").GetInt32(),
                entry.GetProperty("flat_damage_per_die").GetInt32(), Enum.Parse<DamageType>(entry.GetProperty("damage_type").GetString()),
                TimeSpan.FromSeconds(entry.GetProperty("duration_seconds").GetDouble()),
                TimeSpan.FromSeconds(entry.GetProperty("tick_interval_seconds").GetDouble()), reference, sides,
                entry.GetProperty("refresh_duration_only").GetBoolean(), entry.GetProperty("scale_with_source").GetBoolean(),
                entry.GetProperty("movement_slow").GetDecimal()))
            .ToArray();

        if (rules.DistinctBy(rule => rule.Id).Count() != rules.Length
            || rules.DistinctBy(rule => rule.Kind).Count() != rules.Length)
        {
            throw new InvalidOperationException("Production status definitions must have unique identities and kinds.");
        }

        return Array.AsReadOnly(rules);
    }
}
