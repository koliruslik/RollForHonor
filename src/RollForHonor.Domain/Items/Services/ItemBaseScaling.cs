using RollForHonor.Domain.Combat.Damage.Models;
using RollForHonor.Domain.Items.Models;
using RollForHonor.Domain.Stats;

namespace RollForHonor.Domain.Items.Services;

/// <summary>Projects captured base progression without changing affixes, cadence or requirements.</summary>
public static class ItemBaseScaling
{
    /// <summary>Scales numeric base contributions while preserving fractional stat ratings.</summary>
    /// <param name="item">The captured item.</param>
    /// <returns>Base modifiers with eligible flat amounts scaled once.</returns>
    public static IEnumerable<ItemModifier> Modifiers(ItemInstance item) => item.Base.Modifiers
        .Select(modifier => modifier.Operation == StatOperation.Flat && modifier.Key.Type is
            StatType.MaxHealth or StatType.MaxMana or StatType.Armor or StatType.Evasion or StatType.AttackModifier
            or StatType.HealthRecovery or StatType.ManaRecovery or StatType.AddedDamage
            ? new ItemModifier(modifier.Key, modifier.Operation, modifier.Amount * item.Progression.Multiplier) : modifier);

    /// <summary>Retains base dice and applies progression after each actual base roll.</summary>
    /// <param name="item">The captured weapon item.</param>
    /// <returns>Scaled base components, or an empty list for nonweapons.</returns>
    public static IReadOnlyList<DamageComponent> WeaponDamage(ItemInstance item) => item.Base.Weapon?.Damage
        .Select(part => new DamageComponent(part.Dice, part.FlatDamage, part.Type, part.Multiplier * item.Progression.Multiplier))
        .ToArray() ?? [];
}
