namespace RollForHonor.Application.Combat.Models;

/// <summary>Describes whether an adapter atomically published a calculated proposal.</summary>
public enum CombatCommitStatus
{
    /// <summary>The complete proposal was published once.</summary>
    Applied,
    /// <summary>The expected version is stale; no part of the proposal was published.</summary>
    VersionConflict,
    /// <summary>This resolution was already published; nothing was applied again.</summary>
    AlreadyApplied
}
