namespace RollForHonor.Application.Combat.Models;

/// <summary>Describes admission of a contact separately from the Domain outcome and commit status.</summary>
public enum AttackContactStatus
{
    /// <summary>An admitted combatant contact was calculated, or an admitted obstacle contact was handled without combat.</summary>
    Processed,
    /// <summary>The attack had already ended or its lifetime expired; no impact was calculated.</summary>
    AttackEnded,
    /// <summary>The once-per-target rule already consumed this target; no impact was calculated.</summary>
    TargetAlreadyContacted
}

/// <summary>Describes the first condition that ended an attack instance.</summary>
public enum AttackEndReason
{
    /// <summary>The host explicitly ended the attack.</summary>
    Explicit,
    /// <summary>The host reported the end of the attack animation.</summary>
    AnimationFinished,
    /// <summary>Observed game time reached the configured expiry deadline.</summary>
    LifetimeExpired,
    /// <summary>An admitted combatant contact matched the configured termination rule.</summary>
    CombatantContact,
    /// <summary>An obstacle contact matched the configured termination rule.</summary>
    EnvironmentContact
}

/// <summary>Returns contact admission and any calculated impact without confusing ignored contacts with Domain rejection.</summary>
/// <param name="Status">Whether the contact was handled or ignored before calculation.</param>
/// <param name="Impact">The combat calculation/commit response; null for obstacles and ignored contacts.</param>
/// <param name="EndReason">The attack's observed end reason when this response was constructed, or null while active.</param>
public sealed record AttackContactResult(AttackContactStatus Status, CombatImpactResult? Impact, AttackEndReason? EndReason);
