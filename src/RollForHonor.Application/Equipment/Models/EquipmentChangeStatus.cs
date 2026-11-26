namespace RollForHonor.Application.Equipment.Models;

/// <summary>Describes a minimal equipment operation without assuming UI or exception-based gameplay failures.</summary>
public enum EquipmentChangeStatus
{
    /// <summary>Equipment and derived state were published together.</summary>
    Applied,

    /// <summary>The requested state was already current; no version or resource changed.</summary>
    Unchanged,

    /// <summary>The supplied read became stale; no part of the proposal was published.</summary>
    VersionConflict,

    /// <summary>The requested item is not owned by this actor.</summary>
    ItemNotOwned,

    /// <summary>The item's tags do not satisfy the slot requirement.</summary>
    IncompatibleSlot,

    /// <summary>The same owned item is already equipped in another slot.</summary>
    AlreadyEquippedElsewhere,

    /// <summary>The fixed bag cannot contain every item returned by the operation.</summary>
    InventoryFull,

    /// <summary>Level, branch attributes or released content requirements are not satisfied.</summary>
    RequirementsNotMet
}
