using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Godot;
using RollForHonor.Domain.Combat.Effects.Models;
using RollForHonor.Domain.Stats;
using RollForHonor.Godot.CombatDemo;
using RollForHonor.Godot.Content.Combat;
using RollForHonor.Godot.UI.Localization;

namespace RollForHonor.Godot.UI.Common;

/// <summary>Formats authored status rules without duplicating gameplay balance values in UI text.</summary>
public static class StatusEffectPresentation
{
    private static readonly Dictionary<StatusEffectKind, Texture2D> Icons = [];

    /// <summary>Loads the shared vector glyph for a production status.</summary>
    /// <param name="rule">The status whose category selects the icon.</param>
    /// <returns>The cached texture shared by target and overhead indicators.</returns>
    public static Texture2D Icon(OnHitStatusRule rule)
    {
        if (!Icons.TryGetValue(rule.Kind, out var texture))
        {
            texture = GD.Load<Texture2D>($"res://UI/Assets/status-{rule.Kind.ToString().ToLowerInvariant()}.svg");
            Icons.Add(rule.Kind, texture);
        }

        return texture;
    }

    /// <summary>Explains active captured formulas and stack counts alongside the status's general rules.</summary>
    /// <param name="rule">The selected production status rule.</param>
    /// <param name="effects">The current target effects, filtered internally by definition identity.</param>
    /// <returns>A localized inspection card retaining the clicked snapshot's values.</returns>
    public static InspectionTopic ActiveTopic(OnHitStatusRule rule, IReadOnlyList<CombatEffectSnapshot> effects)
    {
        var active = effects
            .Where(effect => effect.Definition.Id == rule.Id)
            .ToArray();
        var facts = active
            .Select(effect => effect.Definition)
            .OfType<PeriodicDamageEffectDefinition>()
            .GroupBy(Captured)
            .Select(group => new InspectionFact(L.Format($"{group.Count()} × {group.Key}"), HonorTheme.Parchment))
            .Prepend(new InspectionFact(L.Format($"Active stacks: {active.Length}"), HonorTheme.Secondary));
        var description = Topic(rule);
        return new(description.Title, description.Description, description.Accent, facts: facts);
    }

    private static string Captured(PeriodicDamageEffectDefinition effect)
    {
        var damage = string.Join(" + ", effect.Damage.Select(L.Damage));
        var timing = L.Format($"{damage} / {effect.TickInterval?.TotalSeconds:0.##} s; captured duration {effect.Duration?.TotalSeconds:0.##} s");
        var scale = effect.DamageMultiplier != 1 ? L.Format($"Status damage multiplier: ×{effect.DamageMultiplier:0.##}") : "";
        var slow = effect.MovementSlow > 0 ? L.Format($"Movement slow: {effect.MovementSlow * 100:0.##}%") : "";
        return string.Join("\n", new[] { timing, scale, slow }.Where(value => value.Length > 0));
    }

    /// <summary>Finds the status whose source statistics include a property.</summary>
    /// <param name="type">A scalar source stat category.</param>
    /// <returns>The configured rule, or null for an unrelated stat.</returns>
    public static OnHitStatusRule ForStat(StatType type) => StatusEffectContent.Rules.FirstOrDefault(rule =>
        rule.Stats.Chance == type || rule.Stats.Guaranteed == type || rule.Stats.Dice == type || rule.Stats.Duration == type
        || rule.Stats.Damage == type || rule.Stats.TickRate == type || rule.Stats.Slow == type);

    /// <summary>Formats the configured dice and signed per-die flat adjustment.</summary>
    /// <param name="rule">The status content definition.</param>
    /// <param name="count">Captured or displayed whole dice count.</param>
    /// <returns>A damage formula, including a negative total for bleeding.</returns>
    public static string Damage(OnHitStatusRule rule, decimal count)
    {
        var flat = count * rule.FlatDamagePerDie;
        return $"{count:0}d{rule.DiceSides}" + (flat == 0 ? "" : flat.ToString("+0;-0", CultureInfo.InvariantCulture));
    }

    /// <summary>Explains duration, timing, defense and reapplication from the active content rule.</summary>
    /// <param name="rule">The status content definition.</param>
    /// <returns>A localized linked inspection card with current base values.</returns>
    public static InspectionTopic Topic(OnHitStatusRule rule)
    {
        var timing = L.Format($"Base duration: {rule.Duration.TotalSeconds:0.##} s; tick interval: {rule.TickInterval.TotalSeconds:0.##} s. First tick occurs after one interval.");
        var chance = L.Format($"Chance values use {rule.ReferenceAttacksPerSecond:0.##} attacks/s as reference. Per-hit chance = base chance × reference speed / accepted attack speed, capped at 100%.");
        var refresh = L.Text(rule.RefreshDurationOnly
            ? "Reapplication refreshes only remaining time. Captured damage, slowing and tick cadence remain unchanged."
            : "Each application adds an independent stack with its own timer; stacks have no count cap.");
        var defense = L.Text(rule.ScaleWithSource
            ? "Captured global and fire damage bonuses increase ticks; fire resistance reduces them. Armor and criticals do not affect ticks."
            : "Armor, resistances, criticals and ordinary hit-damage bonuses do not affect ticks. Dedicated status modifiers can improve damage, duration and frequency.");
        var slow = rule.MovementSlow > 0 ? L.Format($"Base movement slow: {rule.MovementSlow * 100:0.##}%.") : "";
        return new(DemoEffectCatalog.Name(rule.Id), string.Join("\n\n", new[] { timing, chance, refresh, defense, slow }
            .Where(value => value.Length > 0)), HonorTheme.Secondary);
    }
}
