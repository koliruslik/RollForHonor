using RollForHonor.Application.Rewards;
using RollForHonor.Domain.Rewards;
using RollForHonor.Domain.Combat.State.Models;

namespace RollForHonor.Infrastructure.Combat;

public sealed partial class InMemoryCombatState
{
    /// <summary>Captures gold for a confirmed defeat without crediting any wallet.</summary>
    /// <param name="sourceId">The registered defeated actor, unique for this reward.</param>
    /// <param name="amount">The already calculated positive quantity.</param>
    /// <param name="rollContext">Optional matching natural/effective rolls and Luck context.</param>
    /// <exception cref="ArgumentException">Generation provenance does not match the amount.</exception>
    /// <returns>The first captured reward, including on repeated capture attempts.</returns>
    /// <exception cref="ArgumentNullException">The source is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">The amount is not positive.</exception>
    /// <exception cref="KeyNotFoundException">The source is not registered.</exception>
    /// <exception cref="InvalidOperationException">The source is still alive.</exception>
    /// <exception cref="OverflowException">The session version cannot advance.</exception>
    public GoldReward CaptureGoldReward(CombatantId sourceId, long amount, LootGroupRoll? rollContext = null)
    {
        var reward = new GoldReward(sourceId, amount, rollContext);

        lock (_gate)
        {
            if (_data.GoldRewards.TryGetValue(sourceId, out var existing))
            {
                return existing;
            }

            if (!_data.Actors[sourceId].IsDefeated)
            {
                throw new InvalidOperationException("Gold rewards require a confirmed defeat.");
            }

            _data = _data with
            {
                Version = checked(_data.Version + 1),
                GoldRewards = _data.GoldRewards.Add(sourceId, reward)
            };

            return reward;
        }
    }

    /// <summary>Reads an uncollected captured reward without creating or recalculating it.</summary>
    /// <param name="sourceId">The nonnull defeated actor identity.</param>
    /// <returns>The captured reward, or null when absent or already consumed.</returns>
    /// <exception cref="ArgumentNullException">The source is null.</exception>
    public GoldReward? ReadGoldReward(CombatantId sourceId)
    {
        ArgumentNullException.ThrowIfNull(sourceId);

        lock (_gate)
        {
            return _data.CollectedGold.Contains(sourceId) ? null : _data.GoldRewards.GetValueOrDefault(sourceId);
        }
    }

    /// <inheritdoc />
    public GoldWalletRead ReadGoldWallet(CombatantId ownerId)
    {
        ArgumentNullException.ThrowIfNull(ownerId);

        lock (_gate)
        {
            _ = _data.Equipment[ownerId];

            return new GoldWalletRead(_data.Version, ownerId, _data.GoldBalances.GetValueOrDefault(ownerId));
        }
    }

    /// <inheritdoc />
    public GoldPickupStatus TryCollectGold(CombatantId ownerId, CombatantId sourceId, long expectedVersion)
    {
        ArgumentNullException.ThrowIfNull(ownerId);
        ArgumentNullException.ThrowIfNull(sourceId);
        ArgumentOutOfRangeException.ThrowIfNegative(expectedVersion);

        lock (_gate)
        {
            _ = _data.Equipment[ownerId];

            if (_data.CollectedGold.Contains(sourceId))
            {
                return GoldPickupStatus.AlreadyCollected;
            }

            if (_data.Version != expectedVersion)
            {
                return GoldPickupStatus.VersionConflict;
            }

            if (!_data.GoldRewards.TryGetValue(sourceId, out var reward))
            {
                return GoldPickupStatus.RewardNotFound;
            }

            if (_data.Actors[ownerId].IsDefeated)
            {
                return GoldPickupStatus.CollectorDefeated;
            }

            var balance = checked(_data.GoldBalances.GetValueOrDefault(ownerId) + reward.Amount);
            _data = _data with
            {
                Version = checked(_data.Version + 1),
                GoldBalances = _data.GoldBalances.SetItem(ownerId, balance),
                CollectedGold = _data.CollectedGold.Add(sourceId)
            };

            return GoldPickupStatus.Collected;
        }
    }
}
