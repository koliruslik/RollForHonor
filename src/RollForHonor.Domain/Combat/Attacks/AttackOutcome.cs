namespace RollForHonor.Domain.Combat.Attacks;

/// <summary>
/// Describes the quality of a resolved attack roll.
/// </summary>
public enum AttackOutcome
{
    /// <summary>The target completely avoids the attack.</summary>
    Evaded,

    /// <summary>The attack makes weak contact.</summary>
    GlancingHit,

    /// <summary>The attack makes normal contact.</summary>
    Hit,

    /// <summary>The attack makes critical contact.</summary>
    CriticalHit
}
