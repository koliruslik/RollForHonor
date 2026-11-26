using RollForHonor.Domain.Combat.State.Models;

namespace RollForHonor.Application.Rewards;

/// <summary>Publishes wallet credit and captured reward consumption under one version boundary.</summary>
public interface IGoldPickupStateAdapter
{
    /// <summary>Reads the registered actor's current gold balance.</summary>
    /// <param name="ownerId">The nonnull stat-managed actor.</param>
    /// <returns>An immutable balance and session version.</returns>
    /// <exception cref="ArgumentNullException">The owner is null.</exception>
    /// <exception cref="KeyNotFoundException">The owner is not stat-managed.</exception>
    GoldWalletRead ReadGoldWallet(CombatantId ownerId);

    /// <summary>Credits the canonical captured amount and consumes the reward in one publication.</summary>
    /// <param name="ownerId">The living stat-managed collector.</param>
    /// <param name="sourceId">The defeated actor whose reward is being collected.</param>
    /// <param name="expectedVersion">The version of the collector's wallet read.</param>
    /// <returns>The collection result; every rejection preserves balances and reward availability.</returns>
    /// <exception cref="ArgumentNullException">An identity is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">The version is negative.</exception>
    /// <exception cref="KeyNotFoundException">The collector is not stat-managed.</exception>
    /// <exception cref="OverflowException">The balance or session version cannot advance.</exception>
    GoldPickupStatus TryCollectGold(CombatantId ownerId, CombatantId sourceId, long expectedVersion);
}
