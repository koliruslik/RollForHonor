using RollForHonor.Domain.Combat.State.Models;
using RollForHonor.Domain.Rewards;

namespace RollForHonor.Application.Rewards;

/// <summary>Retains the exact gold awarded by one confirmed defeat until world pickup.</summary>
public sealed record GoldReward
{
    /// <summary>Gets the defeated actor identity, unique for this reward in the session.</summary>
    public CombatantId SourceId { get; }

    /// <summary>Gets the positive captured quantity; collection never rolls another amount.</summary>
    public long Amount { get; }

    /// <summary>Gets retained generation provenance, or null for an explicitly authored amount.</summary>
    public LootGroupRoll? RollContext { get; }

    /// <summary>Captures one positive reward without crediting a wallet.</summary>
    /// <param name="sourceId">The registered defeated actor.</param>
    /// <param name="amount">The positive exact gold quantity.</param>
    /// <param name="rollContext">Optional captured gold-group outcome matching this amount.</param>
    /// <exception cref="ArgumentException">The supplied context is not a gold outcome with the same amount.</exception>
    /// <exception cref="ArgumentNullException">The source is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">The amount is not positive.</exception>
    public GoldReward(CombatantId sourceId, long amount, LootGroupRoll? rollContext = null)
    {
        ArgumentNullException.ThrowIfNull(sourceId);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(amount);
        if (rollContext is not null && (rollContext.Outcome.Gold is null || rollContext.GoldAmount != amount))
        {
            throw new ArgumentException("Gold provenance must match the captured amount.", nameof(rollContext));
        }

        SourceId = sourceId;
        Amount = amount;
        RollContext = rollContext;
    }
}
