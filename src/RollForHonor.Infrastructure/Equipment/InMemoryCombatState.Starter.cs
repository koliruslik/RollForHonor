using RollForHonor.Domain.Combat.State.Models;
using RollForHonor.Domain.Items.Equipment.Models;
using RollForHonor.Domain.Items.Models;
using RollForHonor.Domain.Stats;

namespace RollForHonor.Infrastructure.Combat;

public sealed partial class InMemoryCombatState
{
    private readonly HashSet<CombatantId> _starterSelections = [];

    /// <summary>Atomically grants and equips one usable Common starting weapon once for an empty character.</summary>
    /// <param name="ownerId">The stat-managed character.</param>
    /// <param name="weapon">The captured released Common weapon with zero requirements.</param>
    /// <returns>True only for the first accepted choice; repeats publish nothing.</returns>
    /// <exception cref="ArgumentException">The input is not a usable starting weapon.</exception>
    /// <exception cref="KeyNotFoundException">The character is not registered.</exception>
    public bool TrySelectStarterWeapon(CombatantId ownerId, ItemInstance weapon)
    {
        ArgumentNullException.ThrowIfNull(ownerId);
        ArgumentNullException.ThrowIfNull(weapon);

        lock (_gate)
        {
            var equipment = _data.Equipment[ownerId];

            if (_starterSelections.Contains(ownerId) || equipment.OwnedItems.Count != 0)
            {
                return false;
            }

            if (weapon.Rarity != ItemRarity.Common || weapon.Base.Weapon is null || !weapon.Base.Tags.Contains("weapon")
                || weapon.Base.Requirements.Level != 0 || weapon.Base.Requirements.Value != 0
                || !weapon.Base.Requirements.IsMet(new StatCalculator().Calculate(equipment.NonEquipmentProfile)))
            {
                throw new ArgumentException("Choose one released Common weapon with zero requirements.", nameof(weapon));
            }

            EnsureUnowned(weapon.Id);
            var loadout = new EquipmentLoadout([new(EquipmentSlot.Weapon, weapon)]);
            var inventory = equipment with { OwnedItems = equipment.OwnedItems.Add(weapon.Id, weapon) };
            var working = _data with { Equipment = _data.Equipment.SetItem(ownerId, inventory) };
            var updated = ApplyEquipmentStats(working, ownerId, equipment.NonEquipmentProfile, loadout);
            _data = updated;
            _starterSelections.Add(ownerId);

            return true;
        }
    }
}
