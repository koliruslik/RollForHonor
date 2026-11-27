using RollForHonor.Application.Items.Upgrades;
using RollForHonor.Domain.Combat.State.Models;
using RollForHonor.Domain.Items.Models;
using RollForHonor.Domain.Stats;

namespace RollForHonor.Infrastructure.Combat;

public sealed partial class InMemoryCombatState
{
    /// <inheritdoc />
    public ItemUpgradeRead ReadUpgrade(CombatantId owner, string itemId, string resourceId)
    {
        ArgumentNullException.ThrowIfNull(owner);
        ArgumentException.ThrowIfNullOrWhiteSpace(itemId);
        RequireUpgradeResource(resourceId);
        lock (_gate)
        {
            var equipment = _data.Equipment[owner];
            return new(_data.Version, equipment.OwnedItems.GetValueOrDefault(itemId),
                _data.GoldBalances.GetValueOrDefault(owner), GetStats(owner).Get(new(StatType.Luck)), _data.Actors[owner].IsDefeated);
        }
    }

    /// <inheritdoc />
    public ItemUpgradeStatus TryCommitUpgrade(ItemUpgradeQuote quote, ItemInstance replacement, long expectedVersion)
    {
        ArgumentNullException.ThrowIfNull(quote);
        ArgumentNullException.ThrowIfNull(replacement);
        RequireUpgradeResource(quote.Cost.ResourceId);
        if (quote.Status != ItemUpgradeStatus.Ready || quote.Cost.Amount <= 0 || replacement.Id != quote.Item.Id
            || !ReferenceEquals(replacement.Base, quote.Item.Base) || !ReferenceEquals(replacement, quote.Result))
        {
            throw new ArgumentException("A commit requires the quote's captured valid result and positive price.", nameof(replacement));
        }

        lock (_gate)
        {
            if (_data.UpgradeReceipts.Contains(quote.Id))
            {
                return ItemUpgradeStatus.AlreadyApplied;
            }

            var read = ReadUpgrade(quote.Owner, quote.Item.Id, quote.Cost.ResourceId);
            if (read.Version != expectedVersion || !ReferenceEquals(read.Item, quote.Item) || read.Luck != quote.Luck)
            {
                return ItemUpgradeStatus.Stale;
            }

            if (read.Defeated)
            {
                return ItemUpgradeStatus.Defeated;
            }

            if (read.Balance < quote.Cost.Amount)
            {
                return ItemUpgradeStatus.InsufficientFunds;
            }

            var next = StageOwnedItemChange(_data, quote.Owner, quote.Item, replacement);
            _data = next with
            {
                GoldBalances = next.GoldBalances.SetItem(quote.Owner, checked(read.Balance - quote.Cost.Amount)),
                UpgradeReceipts = next.UpgradeReceipts.Add(quote.Id)
            };
            return ItemUpgradeStatus.Applied;
        }
    }

    private static void RequireUpgradeResource(string resourceId)
    {
        if (resourceId != "gold")
        {
            throw new ArgumentException("This session resource adapter currently supports gold only.", nameof(resourceId));
        }
    }
}
