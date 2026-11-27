using RollForHonor.Domain.Combat.State.Models;
using RollForHonor.Domain.Items.Affixes.Models;
using RollForHonor.Domain.Items.Models;

namespace RollForHonor.Application.Items.Upgrades;

/// <summary>Identifies one paid crafting operation.</summary>
public enum ItemUpgradeOperation
{
    /// <summary>Raises enhancement by one.</summary>
    Enhance,
    /// <summary>Raises rarity and carries excess enhancement.</summary>
    Evolve,
    /// <summary>Regenerates every unlocked affix.</summary>
    Reroll,
    /// <summary>Adds exactly one compatible random affix.</summary>
    AddAffix
}

/// <summary>Describes the admission or commit result of a paid operation.</summary>
public enum ItemUpgradeStatus
{
    /// <summary>The operation can be executed.</summary>
    Ready,
    /// <summary>The payment and item were published.</summary>
    Applied,
    /// <summary>This exact quote has already been paid and published.</summary>
    AlreadyApplied,
    /// <summary>The quote no longer matches ownership, stats or the session version.</summary>
    Stale,
    /// <summary>The wallet cannot pay the quoted amount.</summary>
    InsufficientFunds,
    /// <summary>The owner is defeated.</summary>
    Defeated,
    /// <summary>The operation violates a cap, gate or lock rule.</summary>
    Unavailable,
    /// <summary>The eligible pool cannot generate the requested distinct affixes.</summary>
    NoEligibleAffixes,
    /// <summary>The current price table does not support this base generation.</summary>
    UnsupportedBase
}

/// <summary>Captures a resource-identified payment without embedding a wallet in item rules.</summary>
/// <param name="ResourceId">The configured payment resource.</param>
/// <param name="Amount">The positive whole resource amount.</param>
public sealed record ItemUpgradeCost(string ResourceId, long Amount);

/// <summary>Captures a coherent ownership, Luck and wallet view for crafting.</summary>
/// <param name="Version">The session version used only during commit.</param>
/// <param name="Item">The owned snapshot, or null if absent.</param>
/// <param name="Balance">The available resource balance.</param>
/// <param name="Luck">Current resolved Luck.</param>
/// <param name="Defeated">Whether the owner can no longer craft.</param>
public sealed record ItemUpgradeRead(long Version, ItemInstance? Item, long Balance, decimal Luck, bool Defeated);

/// <summary>Reads and atomically publishes crafting through a resource-identified session boundary.</summary>
public interface IItemUpgradeStateAdapter
{
    /// <summary>Reads the owner, exact item and payment balance coherently.</summary>
    /// <param name="owner">The registered owner.</param>
    /// <param name="itemId">The selected owned identity.</param>
    /// <param name="resourceId">The requested payment resource.</param>
    /// <returns>A coherent crafting view.</returns>
    ItemUpgradeRead ReadUpgrade(CombatantId owner, string itemId, string resourceId);

    /// <summary>Publishes a generated replacement and payment once, or changes nothing.</summary>
    /// <param name="quote">The admitted quote, including its unique receipt identity.</param>
    /// <param name="replacement">The captured result retaining identity and base.</param>
    /// <param name="expectedVersion">The version read immediately before generation/commit.</param>
    /// <returns>The atomic commit or rejection status.</returns>
    ItemUpgradeStatus TryCommitUpgrade(ItemUpgradeQuote quote, ItemInstance replacement, long expectedVersion);
}

/// <summary>Supplies enabled, slot-compatible affix content with live Luck-tier thresholds.</summary>
public interface IItemUpgradeAffixSource
{
    /// <summary>Captures eligible definitions without rolling or bypassing item-level gates.</summary>
    /// <param name="item">The original captured item.</param>
    /// <returns>The current compatible content pool.</returns>
    IReadOnlyList<AffixDefinition> GetAffixes(ItemInstance item);
}

/// <summary>Retains a quoted operation and its single captured random result across commit retries.</summary>
public sealed class ItemUpgradeQuote
{
    /// <summary>Gets the unique payment receipt identity.</summary>
    public Guid Id { get; } = Guid.NewGuid();
    /// <summary>Gets the registered owner.</summary>
    public CombatantId Owner { get; }
    /// <summary>Gets the exact selected owned snapshot.</summary>
    public ItemInstance Item { get; }
    /// <summary>Gets the requested operation.</summary>
    public ItemUpgradeOperation Operation { get; }
    /// <summary>Gets the whole, resource-identified fee.</summary>
    public ItemUpgradeCost Cost { get; }
    /// <summary>Gets the quoted current Luck.</summary>
    public decimal Luck { get; }
    /// <summary>Gets the preserved locked affixes.</summary>
    public IReadOnlyList<AffixInstance> Locked { get; }
    /// <summary>Gets admission status; current funds are checked again on execution.</summary>
    public ItemUpgradeStatus Status { get; }
    /// <summary>Gets a deterministic preview, or the random result only after execution.</summary>
    public ItemInstance? Result { get; internal set; }

    internal object Gate { get; } = new();
    internal object Issuer { get; }
    internal IReadOnlyList<AffixDefinition> Pool { get; }

    internal ItemUpgradeQuote(object issuer, CombatantId owner, ItemInstance item, ItemUpgradeOperation operation,
        ItemUpgradeCost cost, decimal luck, AffixInstance[] locked, ItemUpgradeStatus status,
        IReadOnlyList<AffixDefinition> pool, ItemInstance? preview)
    {
        Issuer = issuer;
        Owner = owner;
        Item = item;
        Operation = operation;
        Cost = cost;
        Luck = luck;
        Locked = Array.AsReadOnly(locked);
        Status = status;
        Pool = pool;
        Result = preview;
    }
}
