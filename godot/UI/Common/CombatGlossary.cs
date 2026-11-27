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
        DamageType.Pure => "a4ad96",
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
        if (DamageDiceStats.Sides(key.Type) is var sides && sides > 0)
        {
            return new($"Extra d{sides} {key.DamageKind} damage",
                "Rolls these additional typed dice once per attack impact, before source multipliers, quality and defenses. Periodic ticks do not add them.", StatColor(key));
        }

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

        var status = StatusEffectPresentation.ForStat(key.Type);

        if (status is not null)
        {
            if (key.Type == status.Stats.Guaranteed)
            {
                return new(RollForHonor.Godot.CombatDemo.DemoEffectCatalog.Name(status.Id), L.Text("Guarantees this status on every successful hit, independently of attack speed. Misses do not apply it. Administrative T0 only; it cannot drop."), HonorTheme.Secondary);
            }

            return StatusEffectPresentation.Topic(status);
        }

        return key.Type switch
        {
            StatType.CriticalRollBonus => new(L.Text("Critical roll bonus"), L.Text("Added to a separate critical d20. A result of 20 or more is critical. +3 makes natural 17–20 critical. This bonus does not improve the primary attack roll."), HonorTheme.Brass, [new(L.Text("Critical damage bonus"), HonorTheme.Brass, CriticalDamage), new(L.Text("Hit quality"), HonorTheme.Parchment, HitQuality)]),
            StatType.CharacterLevel => new(L.Text("Level"), L.Text("The current level cap is 10. Level changes do not automatically raise health, mana or combat stats and do not heal. XP and attribute-point awards are not configured yet."), HonorTheme.Brass),
            StatType.Strength => new(L.Text("Strength"), L.Text("Required to equip Strength-branch items. Allocated Strength does not directly increase physical damage."), HonorTheme.Brass),
            StatType.Dexterity => new(L.Text("Dexterity"), L.Text("Required to equip Dexterity-branch items. Allocated Dexterity does not directly add evasion or attack speed."), HonorTheme.Brass),
            StatType.Constitution => new(L.Text("Constitution"), L.Text("Planned role: maximum health and resistances. These allocated points do not change defenses yet; the conversion formulas are not configured."), HonorTheme.Brass),
            StatType.Intelligence => new(L.Text("Intelligence"), L.Text("Required to equip Intelligence-branch items. Allocated Intelligence does not directly add mana or magic damage."), HonorTheme.Brass),
            StatType.Luck => new(L.Text("Luck"), L.Text("Luck improves equipment drop chance and rarity. Sentinel equipment chance is 45% at Luck 0, 48.19% at 10, 59.21% at 50 and 69.75% at 100. Luck also adds up to +5 to affix tier rolls and boosts quality and gold d100 results up to 100. Slot, base, family and occupancy weights stay unchanged; item-level gates still apply."), HonorTheme.Brass),
            StatType.HealthRecovery => new(L.Text("Health recovery"), L.Text("Health points recovered per game second, up to maximum health. Recovery stops on pause and after defeat. This is a flat rate, not a percentage."), HonorTheme.Secondary),
            StatType.ManaRecovery => new(L.Text("Mana recovery"), L.Text("Mana points recovered per game second, up to maximum mana. Recovery stops on pause and after defeat. This is a flat rate, not a percentage."), HonorTheme.Secondary),
            StatType.Armor => Armor(),
            StatType.DamageMultiplier => new(L.Text("All damage"), L.Text("Increases outgoing damage of every type. Increased bonuses add together; More bonuses multiply separately."), HonorTheme.Parchment,
                [new(L.Text("Physical damage"), HonorTheme.Secondary, Physical)]),
            StatType.AttackModifier => Accuracy(),
            StatType.MaxHealth => new(L.Text("Maximum health"), L.Text("The character's health limit. Raising the limit does not heal the character; lowering it clamps current health."), HonorTheme.Parchment),
            StatType.MaxMana => new(L.Text("Maximum mana"), L.Text("The character's mana limit. Raising the limit does not refill mana; lowering it clamps the current balance."), HonorTheme.Parchment),
            StatType.AttackSpeed => new(L.Text("Attack speed"), L.Text("Multiplies the equipped weapon attack cadence. Increased bonuses add together; mana cost per attack stays the same."), HonorTheme.Parchment),
            StatType.MovementSpeed => new(L.Text("Movement speed"), L.Text("Character movement speed. The character sheet shows its percentage change from the unmodified starting speed."), HonorTheme.Parchment),
            StatType.CriticalChance => new(L.Text("Critical chance"), L.Text("Reserved stat. The current attack rules use d20 quality; this independent chance is not rolled by combat yet."), HonorTheme.Parchment),
            StatType.CriticalMultiplier => CriticalDamage(),
            StatType.Evasion => Evasion(),
            _ => new(key.Type.ToString(), L.Text("This property contributes to the character's resolved statistics."), HonorTheme.Parchment)
        };
    }

    /// <summary>Explains how current accuracy contributes to the primary attack roll.</summary>
    /// <returns>A card linked to evasion and hit quality.</returns>
    public static InspectionTopic Accuracy() => new(L.Text("Accuracy"),
        L.Text("Accuracy improves the primary d20 roll, not damage. The character contribution is rounded down to an integer bonus and added to the attack's own bonus. Natural 1 still misses; natural 20 still has full hit quality."),
        HonorTheme.Parchment, [new(L.Text("Evasion"), HonorTheme.Parchment, Evasion), new(L.Text("Hit quality"), HonorTheme.Parchment, HitQuality)]);

    /// <summary>Explains the fractional evasion rating and its comparison to incoming accuracy.</summary>
    /// <returns>A card linked to accuracy, hit quality and armor.</returns>
    public static InspectionTopic Evasion() => new(L.Text("Evasion"),
        L.Text("Evasion raises the glancing threshold: 6 + evasion / 10. On natural 2–19, a lower d20 + attack bonus deals half damage; otherwise it deals full damage. Evasion 100 gives threshold 16: against a +4 roll, natural 2–11 glance and 12–20 hit fully. Only natural 1 fully misses. Fractional evasion counts."),
        HonorTheme.Parchment, [new(L.Text("Accuracy"), HonorTheme.Parchment, Accuracy), new(L.Text("Hit quality"), HonorTheme.Parchment, HitQuality), new(L.Text("Armor"), HonorTheme.Secondary, Armor)]);

    /// <summary>Explains primary hit quality separately from the independent critical check.</summary>
    /// <returns>A card linked to the defenses applied after quality.</returns>
    public static InspectionTopic HitQuality() => new(L.Text("Hit quality"),
        L.Text("Natural 1: miss, zero damage. Natural 20: full quality. On 2–19, d20 + attack bonus below 6 + evasion / 10 gives a glancing hit at 50% damage; otherwise 100%. Each component rounds up after quality. A separate critical roll can then increase damage before armor and resistances."),
        HonorTheme.Parchment, [new(L.Text("Armor"), HonorTheme.Secondary, Armor), new(L.Text("Critical damage bonus"), HonorTheme.Brass, CriticalDamage)]);

    /// <summary>Displays the critical coefficient as additional damage above an ordinary hit.</summary>
    /// <returns>A card linked to the independent critical roll and defenses.</returns>
    public static InspectionTopic CriticalDamage() => new(L.Text("Critical damage bonus"),
        L.Text("Additional damage on a successful independent critical roll. +100% means double damage; +150% means 2.5 times damage. The displayed bonus is (critical multiplier - 1) × 100%. It applies after primary hit quality and before armor and resistances; it does not increase critical chance."),
        HonorTheme.Brass, [Link(new(StatType.CriticalRollBonus)), new(L.Text("Hit quality"), HonorTheme.Parchment, HitQuality), new(L.Text("Armor"), HonorTheme.Secondary, Armor)]);

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
        L.Text("Reduces physical hit damage only. The reduction is armor / (armor + 30 × physical hit damage). Larger hits receive less proportional protection.\n\nTyped resistances apply afterward. Armor does not reduce fire, cold or lightning damage."),
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
