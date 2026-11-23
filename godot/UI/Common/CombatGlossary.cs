using System;
using Godot;
using RollForHonor.Domain.Combat.Damage.Extensions;
using RollForHonor.Domain.Combat.Damage.Models;
using RollForHonor.Domain.Stats;
using RollForHonor.Godot.UI.Localization;

namespace RollForHonor.Godot.UI.Common;

/// <summary>Maps the game's actual damage and defense rules to shared semantic colors and linked explanations.</summary>
public static class CombatGlossary
{
    /// <summary>Gets a distinct color for each authored damage category.</summary>
    /// <param name="type">The category.</param>
    /// <returns>A readable accent on the dark interface.</returns>
    /// <exception cref="ArgumentOutOfRangeException">The category is undefined.</exception>
    public static Color DamageColor(DamageType type) => new(type switch
    {
        DamageType.Piercing => "b7b9bc",
        DamageType.Bludgeoning => "b8b0a3",
        DamageType.Slashing => "b7aaad",
        DamageType.Fire => "be9e90",
        DamageType.Cold => "9eafb5",
        DamageType.Lightning => "b9b092",
        DamageType.Poison => "a4ad96",
        _ => throw new ArgumentOutOfRangeException(nameof(type))
    });

    /// <summary>Uses the same color for outgoing damage and its corresponding resistance.</summary>
    /// <param name="key">The displayed stat.</param>
    /// <returns>The typed color, or neutral parchment for untyped properties.</returns>
    public static Color StatColor(StatKey key) => key.DamageKind is { } type ? DamageColor(type) : HonorTheme.Parchment;

    /// <summary>Creates a related-stat link without instantiating its next card.</summary>
    /// <param name="key">The concept to explain.</param>
    /// <returns>A lazy semantic link.</returns>
    public static InspectionLink Link(StatKey key) => new(Stat(key).Title, StatColor(key), () => Stat(key));

    /// <summary>Explains the displayed stat using current project mechanics.</summary>
    /// <param name="key">The stat category and optional damage type.</param>
    /// <returns>A linked explanation; reserved mechanics are identified explicitly.</returns>
    public static InspectionTopic Stat(StatKey key)
    {
        if (key.DamageKind is { } type)
        {
            if (key.Type == StatType.AddedDamage)
            {
                return new(L.Format($"Added {type.ToString().ToLowerInvariant()} damage"),
                    L.Text("Adds a flat amount to attack damage before outgoing multipliers, attack quality and defenses. It is not added again to periodic effect ticks."),
                    DamageColor(type), [new(L.Format($"{type} damage"), DamageColor(type), () => Damage(type))]);
            }

            return key.Type == StatType.Resistance ? Resistance(type) : Damage(type);
        }

        return key.Type switch
        {
            StatType.Strength or StatType.Dexterity or StatType.Constitution or StatType.Intelligence or StatType.Luck =>
                new(L.Text(key.Type.ToString()), L.Text("Allocated attribute points. Derived combat and loot bonuses are not configured yet."), HonorTheme.Brass),
            StatType.Armor => Armor(),
            StatType.DamageMultiplier => new(L.Text("All damage"), L.Text("Increases outgoing damage of every type. Increased bonuses add together; More bonuses multiply separately."), HonorTheme.Parchment,
                [new(L.Text("Physical damage"), HonorTheme.Secondary, Physical)]),
            StatType.AttackModifier => new(L.Text("Attack roll bonus"), L.Text("Added to the attack's d20 roll. It affects attack quality and is not a flat damage bonus."), HonorTheme.Parchment),
            StatType.MaxHealth => new(L.Text("Maximum health"), L.Text("The character's health limit. Raising the limit does not heal the character; lowering it clamps current health."), HonorTheme.Parchment),
            StatType.MaxMana => new(L.Text("Maximum mana"), L.Text("The character's mana limit. Raising the limit does not refill mana; lowering it clamps the current balance."), HonorTheme.Parchment),
            StatType.MovementSpeed => new(L.Text("Movement speed"), L.Text("Character movement speed. The character sheet shows its percentage change from the unmodified starting speed."), HonorTheme.Parchment),
            StatType.CriticalChance => new(L.Text("Critical chance"), L.Text("Reserved stat. The current attack rules use d20 quality; this independent chance is not rolled by combat yet."), HonorTheme.Parchment),
            StatType.CriticalMultiplier => new(L.Text("Critical multiplier"), L.Text("Reserved stat. Current critical damage uses the attack-quality coefficients rather than this independent multiplier."), HonorTheme.Parchment),
            StatType.Evasion => new(L.Text("Evasion"), L.Text("A defensive stat used when resolving attack quality against the attack roll."), HonorTheme.Parchment),
            _ => new(key.Type.ToString(), L.Text("This property contributes to the character's resolved statistics."), HonorTheme.Parchment)
        };
    }

    /// <summary>Explains a damage type without importing status rules from other games.</summary>
    /// <param name="type">The damage category.</param>
    /// <returns>Damage classification and related defenses.</returns>
    public static InspectionTopic Damage(DamageType type)
    {
        var classification = type.IsPhysical()
            ? L.Text("Physical damage. Armor reduces the combined physical hit first, then the matching resistance applies.")
            : L.Format($"Nonphysical damage. Armor does not reduce it; {type.ToString().ToLowerInvariant()} resistance applies instead.");

        return new(L.Format($"{type} damage"), L.Format($"{classification}{L.Text("\n\nA damage type alone does not apply a status effect.")}"), DamageColor(type),
        [
            new(L.Format($"{type} resistance"), DamageColor(type), () => Resistance(type)),
            type.IsPhysical() ? new(L.Text("Armor"), HonorTheme.Secondary, Armor)
                : new(L.Text("Physical damage"), HonorTheme.Secondary, Physical)
        ]);
    }

    /// <summary>Explains typed resistance, including physical resistance and weaknesses.</summary>
    /// <param name="type">The resisted damage category.</param>
    /// <returns>The resistance description and a link back to that damage type.</returns>
    public static InspectionTopic Resistance(DamageType type)
    {
        var defenseOrder = type.IsPhysical()
            ? L.Text("Applies after armor. A positive physical hit retains at least one point of total physical damage.")
            : L.Text("Armor does not reduce this damage type.");

        return new(L.Format($"{type} resistance"),
            L.Format($"Reduces incoming {type.ToString().ToLowerInvariant()} damage. 25% resistance leaves 75% of that damage before rounding. Negative resistance increases it. Resistance is capped at 100%.\n\n{defenseOrder}"),
            DamageColor(type), [new(L.Format($"{type} damage"), DamageColor(type), () => Damage(type))]);
    }

    /// <summary>Explains the armor formula used by the current combat pipeline.</summary>
    /// <returns>An armor explanation linked to physical types.</returns>
    public static InspectionTopic Armor() => new(L.Text("Armor"),
        L.Text("Reduces physical hit damage only. The reduction is armor / (armor + 5 × physical hit damage). Larger hits receive less proportional protection.\n\nTyped resistances apply afterward. Armor does not reduce fire, cold, lightning or poison damage."),
        HonorTheme.Secondary, [new(L.Text("Physical damage"), HonorTheme.Secondary, Physical)]);

    /// <summary>Explains the shared physical classification.</summary>
    /// <returns>Links to all three physical types and armor.</returns>
    public static InspectionTopic Physical() => new(L.Text("Physical damage"),
        L.Text("Piercing, bludgeoning and slashing are physical damage. Armor is applied to their combined hit, followed by each type's resistance."), HonorTheme.Secondary,
        [
            new(L.Text("Piercing damage"), DamageColor(DamageType.Piercing), () => Damage(DamageType.Piercing)),
            new(L.Text("Bludgeoning damage"), DamageColor(DamageType.Bludgeoning), () => Damage(DamageType.Bludgeoning)),
            new(L.Text("Slashing damage"), DamageColor(DamageType.Slashing), () => Damage(DamageType.Slashing)),
            new(L.Text("Armor"), HonorTheme.Secondary, Armor)
        ]);
}
