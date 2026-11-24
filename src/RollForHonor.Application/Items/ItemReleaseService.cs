using RollForHonor.Domain.Combat.State.Models;
using RollForHonor.Domain.Items.Models;

namespace RollForHonor.Application.Items;

/// <summary>Coordinates one version-checked release from the bag into the world.</summary>
public sealed class ItemReleaseService
{
    private readonly IItemReleaseStateAdapter _state;

    /// <summary>Uses the session ownership port.</summary>
    /// <param name="state">The atomic release port.</param>
    /// <exception cref="ArgumentNullException">The port is null.</exception>
    public ItemReleaseService(IItemReleaseStateAdapter state)
    {
        ArgumentNullException.ThrowIfNull(state);
        _state = state;
    }

    /// <summary>Releases an owned bag item without changing its identity or rolls.</summary>
    /// <param name="owner">The registered owner.</param>
    /// <param name="itemId">The instance to release.</param>
    /// <returns>The released item, or null if it cannot leave the bag.</returns>
    /// <exception cref="ArgumentNullException">The owner is null.</exception>
    /// <exception cref="ArgumentException">The item identity is blank.</exception>
    /// <exception cref="KeyNotFoundException">The owner is not registered.</exception>
    /// <exception cref="OverflowException">The session version cannot advance.</exception>
    public ItemInstance? Release(CombatantId owner, string itemId)
    {
        ArgumentNullException.ThrowIfNull(owner);
        ArgumentException.ThrowIfNullOrWhiteSpace(itemId);
        var read = _state.ReadEquipment(owner);

        return _state.TryReleaseItem(owner, itemId, read.Version);
    }
}
