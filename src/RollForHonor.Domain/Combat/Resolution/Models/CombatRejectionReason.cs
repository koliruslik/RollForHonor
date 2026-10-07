namespace RollForHonor.Domain.Combat.Resolution.Models;

/// <summary>
/// Identifies why a combat request cannot be resolved.
/// </summary>
public enum CombatRejectionReason
{
    SourceNotFound,
    TargetNotFound,
    SourceDefeated,
    InvalidAttack,
    InvalidState
}
