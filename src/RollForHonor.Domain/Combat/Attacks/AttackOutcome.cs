namespace RollForHonor.Domain.Combat.Attacks;

/// <summary>
/// Describes the quality of a resolved attack roll.
/// </summary>
public enum AttackOutcome
{
    CriticalFailure,
    Miss,
    GlancingHit,
    Hit,
    CriticalSuccess
}
