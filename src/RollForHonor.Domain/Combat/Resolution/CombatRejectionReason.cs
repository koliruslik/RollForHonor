namespace RollForHonor.Domain.Combat.Resolution;

public enum CombatRejectionReason
{
    SourceNotFound,
    TargetNotFound,
    SourceDefeated,
    InvalidAttack,
    InvalidState
}
