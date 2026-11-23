using RollForHonor.Domain.Items.Equipment.Configuration;
using RollForHonor.Domain.Items.Equipment.Models;
using RollForHonor.Domain.Items.Models;

namespace RollForHonor.Domain.Items.Equipment.Services;

/// <summary>Creates loadout changes without managing ownership, combat state or resource balances.</summary>
public sealed class EquipmentLoadoutService
{
    private readonly EquipmentRules _rules;

    /// <summary>Uses explicit item-tag compatibility rules.</summary>
    /// <param name="rules">The immutable slot requirements.</param>
    /// <exception cref="ArgumentNullException">The rules are null.</exception>
    public EquipmentLoadoutService(EquipmentRules rules)
    {
        ArgumentNullException.ThrowIfNull(rules);

        _rules = rules;
    }

    /// <summary>Replaces a slot and removes an offhand made incompatible by a main-hand change.</summary>
    /// <param name="loadout">The immutable original equipment.</param>
    /// <param name="item">The item to equip.</param>
    /// <param name="slot">The defined target slot.</param>
    /// <returns>A new loadout, or the original when that exact item is already in that slot.</returns>
    /// <exception cref="ArgumentNullException">An input is null.</exception>
    /// <exception cref="ArgumentException">The item is incompatible or already occupies another slot.</exception>
    /// <exception cref="ArgumentOutOfRangeException">The slot is undefined.</exception>
    public EquipmentLoadout Equip(EquipmentLoadout loadout, ItemInstance item, EquipmentSlot slot)
    {
        ArgumentNullException.ThrowIfNull(loadout);
        ArgumentNullException.ThrowIfNull(item);

        if (!_rules.CanEquip(loadout, item, slot))
        {
            throw new ArgumentException("The item is incompatible with this slot.", nameof(item));
        }

        if (loadout.Slots.Any(pair => pair.Key != slot && pair.Value.Id == item.Id))
        {
            throw new ArgumentException("The item already occupies another slot.", nameof(item));
        }

        if (ReferenceEquals(loadout.GetItem(slot), item))
        {
            return loadout;
        }

        var slots = loadout.Slots
            .Where(pair => pair.Key != slot)
            .Append(new KeyValuePair<EquipmentSlot, ItemInstance>(slot, item));

        return RemoveIncompatibleOffHand(new EquipmentLoadout(slots));
    }

    /// <summary>Empties a slot and removes an offhand which requires the removed main-hand weapon.</summary>
    /// <param name="loadout">The immutable original equipment.</param>
    /// <param name="slot">The defined slot to empty.</param>
    /// <returns>A new loadout, or the original when the slot was already empty.</returns>
    /// <exception cref="ArgumentNullException">The loadout is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">The slot is undefined.</exception>
    public EquipmentLoadout Unequip(EquipmentLoadout loadout, EquipmentSlot slot)
    {
        ArgumentNullException.ThrowIfNull(loadout);

        if (loadout.GetItem(slot) is null)
        {
            return loadout;
        }

        var retained = loadout.Slots
            .Where(pair => pair.Key != slot);

        return RemoveIncompatibleOffHand(new EquipmentLoadout(retained));
    }

    private EquipmentLoadout RemoveIncompatibleOffHand(EquipmentLoadout loadout)
    {
        var offHand = loadout.GetItem(EquipmentSlot.OffHand);

        if (offHand is null || _rules.CanEquip(loadout, offHand, EquipmentSlot.OffHand))
        {
            return loadout;
        }

        var retained = loadout.Slots
            .Where(pair => pair.Key != EquipmentSlot.OffHand);

        return new EquipmentLoadout(retained);
    }
}
