using RollForHonor.Domain.Combat.State.Models;
using RollForHonor.Domain.Items.Equipment.Models;
using RollForHonor.Domain.Items.Models;

namespace RollForHonor.Infrastructure.Combat;

public sealed partial class InMemoryCombatState
{
    /// <summary>Atomically replaces an administratively edited owned item and recomposes equipped contributions.</summary>
    /// <param name="ownerId">The registered stat-managed owner.</param>
    /// <param name="expected">The exact owned snapshot originally opened by the editor.</param>
    /// <param name="replacement">The validated replacement retaining identity and captured base.</param>
    /// <returns>True when published; false if the item was removed or edited since opening.</returns>
    /// <exception cref="ArgumentNullException">An argument is null.</exception>
    /// <exception cref="ArgumentException">Identity or base changes, or equipped rules would be violated.</exception>
    /// <exception cref="OverflowException">Stat arithmetic or the session version overflows; no change is published.</exception>
    /// <remarks>Other combat updates do not invalidate the draft. Bag placement is retained because the base and footprint cannot change.</remarks>
    public bool TryEditOwnedItem(CombatantId ownerId, ItemInstance expected, ItemInstance replacement)
    {
        ArgumentNullException.ThrowIfNull(ownerId);
        ArgumentNullException.ThrowIfNull(expected);
        ArgumentNullException.ThrowIfNull(replacement);

        if (expected.Id != replacement.Id || !ReferenceEquals(expected.Base, replacement.Base))
        {
            throw new ArgumentException("Item editing must retain identity and the captured base.", nameof(replacement));
        }

        lock (_gate)
        {
            var equipment = _data.Equipment[ownerId];

            if (!equipment.OwnedItems.TryGetValue(expected.Id, out var current) || !ReferenceEquals(current, expected))
            {
                return false;
            }

            _data = StageOwnedItemChange(_data, ownerId, expected, replacement);
            return true;
        }
    }

    private SessionData StageOwnedItemChange(SessionData data, CombatantId ownerId, ItemInstance expected, ItemInstance replacement)
    {
        var equipment = data.Equipment[ownerId];
        var loadout = new EquipmentLoadout(equipment.Loadout.Slots
            .Select(pair => new KeyValuePair<EquipmentSlot, ItemInstance>(
                pair.Key, pair.Value.Id == expected.Id ? replacement : pair.Value)));
        var updated = equipment with { OwnedItems = equipment.OwnedItems.SetItem(expected.Id, replacement), Loadout = loadout };
        ValidateLoadout(updated, loadout);
        var next = data with { Equipment = data.Equipment.SetItem(ownerId, updated) };

        return equipment.Loadout.Slots.Values.Any(item => item.Id == expected.Id)
            ? ApplyEquipmentStats(next, ownerId, equipment.NonEquipmentProfile, loadout)
            : next with { Version = checked(data.Version + 1) };
    }
}
