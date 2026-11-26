using RollForHonor.Domain.Combat.State.Models;

namespace RollForHonor.Application.Rewards;

/// <summary>Reads one wallet at the shared session version.</summary>
public sealed record GoldWalletRead
{
    /// <summary>Gets the version required by an optimistic collection attempt.</summary>
    public long Version { get; }

    /// <summary>Gets the registered stat-managed wallet owner.</summary>
    public CombatantId OwnerId { get; }

    /// <summary>Gets the current nonnegative exact gold balance.</summary>
    public long Balance { get; }

    /// <summary>Captures an immutable wallet read.</summary>
    /// <param name="version">The nonnegative session version.</param>
    /// <param name="ownerId">The nonnull wallet owner.</param>
    /// <param name="balance">The nonnegative balance.</param>
    /// <exception cref="ArgumentNullException">The owner is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">The version or balance is negative.</exception>
    public GoldWalletRead(long version, CombatantId ownerId, long balance)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(version);
        ArgumentNullException.ThrowIfNull(ownerId);
        ArgumentOutOfRangeException.ThrowIfNegative(balance);
        Version = version;
        OwnerId = ownerId;
        Balance = balance;
    }
}
