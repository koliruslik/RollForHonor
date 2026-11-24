using RollForHonor.Domain.Combat.State.Models;
using RollForHonor.Domain.Items.Models;
using RollForHonor.Domain.Items.Equipment.Services;

namespace RollForHonor.Infrastructure.Combat;

public sealed partial class InMemoryCombatState
{
    /// <inheritdoc />
    public ItemInstance? TryReleaseItem(CombatantId owner, string itemId, long expectedVersion)
    {
        ArgumentNullException.ThrowIfNull(owner);
        ArgumentException.ThrowIfNullOrWhiteSpace(itemId);

        lock (_gate)
        {
            var equipment = _data.Equipment[owner];

            if (expectedVersion != _data.Version || _data.Actors[owner].IsDefeated
                || !equipment.OwnedItems.TryGetValue(itemId, out var item))
            {
                return null;
            }

            var slot = equipment.Loadout.Slots.FirstOrDefault(pair => pair.Value.Id == itemId);
            var next = slot.Value is null ? _data : ApplyEquipmentStats(_data, owner, equipment.NonEquipmentProfile,
                new EquipmentLoadoutService(_equipmentRules).Unequip(equipment.Loadout, slot.Key));
            var released = next.Equipment[owner] with { OwnedItems = equipment.OwnedItems.Remove(itemId) };
            _data = next with
            {
                Version = checked(_data.Version + 1),
                Equipment = next.Equipment.SetItem(owner, released)
            };

            return item;
        }
    }
}
