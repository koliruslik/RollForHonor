namespace RollForHonor.Application.Combat.Models;

/// <summary>Describes attack acceptance separately from later hit outcomes.</summary>
public enum AttackStartStatus
{
    /// <summary>Costs and cooldown were committed; the returned attack may now be launched.</summary>
    Started,
    /// <summary>The source cannot start an attack while defeated.</summary>
    SourceDefeated,
    /// <summary>The cooldown has not expired.</summary>
    OnCooldown,
    /// <summary>At least one calculated resource cost is unaffordable.</summary>
    InsufficientResources,
    /// <summary>The initiation read became stale; nothing from this attempt was committed.</summary>
    VersionConflict,
    /// <summary>This request was already accepted; do not spawn another attack.</summary>
    AlreadyStarted
}
