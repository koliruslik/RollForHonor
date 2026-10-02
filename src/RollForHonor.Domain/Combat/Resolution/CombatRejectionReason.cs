namespace RollForHonor.Domain.Combat.Resolution;

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
