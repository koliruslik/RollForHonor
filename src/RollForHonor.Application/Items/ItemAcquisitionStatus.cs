namespace RollForHonor.Application.Items;

/// <summary>Describes a capacity-checked attempt to acquire one immutable item.</summary>
public enum ItemAcquisitionStatus
{
    /// <summary>The item became owned exactly once.</summary>
    Acquired,
    /// <summary>No free rectangle fits the item in the current bag.</summary>
    InventoryFull,
    /// <summary>This instance is already owned in the session.</summary>
    AlreadyOwned,
    /// <summary>The session changed before publication; no item was granted.</summary>
    VersionConflict,
    /// <summary>The defeated owner cannot pick up items.</summary>
    OwnerDefeated
}
