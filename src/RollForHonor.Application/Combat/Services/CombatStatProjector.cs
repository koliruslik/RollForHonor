using RollForHonor.Domain.Combat.Damage.Models;
using RollForHonor.Domain.Combat.State.Models;
using RollForHonor.Domain.Stats;
using RollForHonor.Domain.Dice.Models;

namespace RollForHonor.Application.Combat.Services;

/// <summary>Projects already-resolved character values into the typed, immutable combat input contract.</summary>
public sealed class CombatStatProjector
{
    /// <summary>Preserves identity/effects/current HP and replaces derived combat inputs.</summary>
    /// <param name="current">The existing state at the owning adapter's consistent version.</param>
    /// <param name="stats">Resolved values from the Domain calculator.</param>
    /// <returns>A combat view; current HP is clamped downward to the new maximum, never healed by a stat increase.</returns>
    /// <exception cref="ArgumentNullException">An input is null.</exception>
    /// <exception cref="OverflowException">An integer-valued combat attribute exceeds Int32 range.</exception>
    public CombatantSnapshot Project(CombatantSnapshot current, StatBlock stats)
    {
        ArgumentNullException.ThrowIfNull(current);
        ArgumentNullException.ThrowIfNull(stats);
        var maxHealth = checked((int)decimal.Floor(stats.Get(new(StatType.MaxHealth))));
        var resistances = DamageTypes.HitTypes
            .Select(type => new ResistanceValue(type, stats.Get(new(StatType.Resistance, type))))
            .Where(value => value.Value != 0)
            .ToArray();
        var offense = new CombatOffense(
            checked((int)decimal.Floor(stats.Get(new(StatType.AttackModifier)))),
            DamageTypes.HitTypes
                .Select(type => new KeyValuePair<DamageType, decimal>(type, stats.Get(new(StatType.DamageMultiplier, type)))),
            DamageTypes.HitTypes
                .Select(type => new DamageAmount(type, checked((int)decimal.Floor(stats.Get(new(StatType.AddedDamage, type)))))),
            DamageDiceStats.Types
                .SelectMany(type => DamageTypes.HitTypes
                    .Select(damage => (Type: damage, Sides: DamageDiceStats.Sides(type), Count: checked((int)decimal.Floor(stats.Get(new(type, damage)))))))
                .Where(value => value.Count > 0)
                .Select(value => new DamageComponent(new DiceFormula(value.Count, value.Sides), 0, value.Type)),
            checked((int)decimal.Floor(stats.Get(new(StatType.CriticalRollBonus)))),
            stats.Get(new(StatType.CriticalMultiplier)),
            stats.Get(new(StatType.PoisonChance)), checked((int)decimal.Floor(stats.Get(new(StatType.PoisonDice)))), stats);

        return new CombatantSnapshot(current.Id, Math.Min(current.Health, maxHealth), maxHealth,
            checked((int)decimal.Floor(stats.Get(new(StatType.Armor)))),
            checked((int)decimal.Floor(stats.Get(new(StatType.Evasion)))),
            resistances, current.Effects, offense, stats.Get(new(StatType.Evasion)));
    }
}
