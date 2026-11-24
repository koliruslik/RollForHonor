using RollForHonor.Domain.Combat.State.Models;
using RollForHonor.Domain.Items.Models;

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
                || !equipment.OwnedItems.TryGetValue(itemId, out var item)
                || equipment.Loadout.Slots.Values.Any(value => value.Id == itemId))
            {
                return null;
            }

            _data = _data with
            {
                Version = checked(_data.Version + 1),
                Equipment = _data.Equipment.SetItem(owner, equipment with { OwnedItems = equipment.OwnedItems.Remove(itemId) })
            };

            return item;
        }
    }
}
