using RollForHonor.Application.Items;
using RollForHonor.Domain.Combat.State.Models;
using RollForHonor.Domain.Items.Models;

namespace RollForHonor.Infrastructure.Combat;

public sealed partial class InMemoryCombatState
{
    /// <inheritdoc />
    public ItemAcquisitionStatus TryGrantItem(CombatantId owner, ItemInstance item, long expectedVersion)
    {
        ArgumentNullException.ThrowIfNull(owner);
        ArgumentNullException.ThrowIfNull(item);

        lock (_gate)
        {
            if (expectedVersion != _data.Version)
            {
                return ItemAcquisitionStatus.VersionConflict;
            }

            var inventory = _data.Equipment[owner];

            if (_data.Actors[owner].IsDefeated)
            {
                return ItemAcquisitionStatus.OwnerDefeated;
            }

            if (_data.Equipment.Values.Any(value => value.OwnedItems.ContainsKey(item.Id)))
            {
                return ItemAcquisitionStatus.AlreadyOwned;
            }

            _data = _data with
            {
                Version = checked(_data.Version + 1),
                Equipment = _data.Equipment.SetItem(owner, inventory with { OwnedItems = inventory.OwnedItems.Add(item.Id, item) })
            };

            return ItemAcquisitionStatus.Acquired;
        }
    }
}
