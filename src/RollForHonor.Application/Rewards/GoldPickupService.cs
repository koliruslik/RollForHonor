using RollForHonor.Domain.Combat.State.Models;

namespace RollForHonor.Application.Rewards;

/// <summary>Attempts one pickup of an already calculated reward without rerolling or retrying.</summary>
public sealed class GoldPickupService
{
    private readonly IGoldPickupStateAdapter _state;

    /// <summary>Uses the shared atomic wallet/reward port.</summary>
    /// <param name="state">The nonnull collection adapter.</param>
    /// <exception cref="ArgumentNullException">The adapter is null.</exception>
    public GoldPickupService(IGoldPickupStateAdapter state)
    {
        ArgumentNullException.ThrowIfNull(state);
        _state = state;
    }

    /// <summary>Collects a captured reward after the world host verifies pickup range.</summary>
    /// <param name="ownerId">The stat-managed collector.</param>
    /// <param name="sourceId">The defeated actor whose captured reward is requested.</param>
    /// <returns>The atomic collection result; conflicts are returned without retry.</returns>
    /// <exception cref="ArgumentNullException">An identity is null.</exception>
    /// <exception cref="KeyNotFoundException">The collector is not stat-managed.</exception>
    /// <exception cref="InvalidOperationException">The port returns a wallet for another owner.</exception>
    /// <exception cref="OverflowException">The balance or session version cannot advance.</exception>
    public GoldPickupStatus Collect(CombatantId ownerId, CombatantId sourceId)
    {
        ArgumentNullException.ThrowIfNull(ownerId);
        ArgumentNullException.ThrowIfNull(sourceId);
        var read = _state.ReadGoldWallet(ownerId);

        if (read is null || read.OwnerId != ownerId)
        {
            throw new InvalidOperationException("The wallet read does not match the collector.");
        }

        return _state.TryCollectGold(ownerId, sourceId, read.Version);
    }
}
