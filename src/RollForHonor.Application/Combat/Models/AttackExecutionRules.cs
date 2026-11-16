namespace RollForHonor.Application.Combat.Models;

/// <summary>Selects whether separate contacts with the same target may calculate another impact.</summary>
public enum AttackRepeatPolicy
{
    /// <summary>Every contact forwarded by the host may calculate an impact.</summary>
    EveryContact,
    /// <summary>The first admitted contact consumes this target, regardless of damage or later calculation/commit failure.</summary>
    OncePerTarget
}

/// <summary>Selects which admitted contact kinds end the attack.</summary>
[Flags]
public enum AttackStopCondition
{
    /// <summary>Contacts do not end the attack.</summary>
    None = 0,
    /// <summary>The first admitted combatant contact ends the attack, including an evaded hit.</summary>
    CombatantContact = 1,
    /// <summary>A non-combatant obstacle contact ends the attack.</summary>
    EnvironmentContact = 2
}

/// <summary>Engine-independent contact and lifetime configuration; defaults preserve unrestricted impact calls.</summary>
public sealed record AttackExecutionRules
{
    /// <summary>Gets whether a previously contacted target can be processed again.</summary>
    public AttackRepeatPolicy RepeatPolicy { get; }
    /// <summary>Gets the contact kinds that terminate this instance.</summary>
    public AttackStopCondition StopOn { get; }
    /// <summary>Gets the optional positive lifetime measured on the host's game clock.</summary>
    public TimeSpan? MaxLifetime { get; }

    /// <summary>Creates immutable execution rules without starting any timer.</summary>
    /// <param name="repeatPolicy">The rule for separate contacts with one target.</param>
    /// <param name="stopOn">Any combination of supported termination contact flags.</param>
    /// <param name="maxLifetime">Optional strictly positive game-time duration.</param>
    /// <exception cref="ArgumentOutOfRangeException">A policy/flag is unknown or lifetime is nonpositive.</exception>
    public AttackExecutionRules(AttackRepeatPolicy repeatPolicy = AttackRepeatPolicy.EveryContact,
        AttackStopCondition stopOn = AttackStopCondition.None, TimeSpan? maxLifetime = null)
    {
        if (!Enum.IsDefined(repeatPolicy)) throw new ArgumentOutOfRangeException(nameof(repeatPolicy));
        if ((stopOn & ~(AttackStopCondition.CombatantContact | AttackStopCondition.EnvironmentContact)) != 0)
            throw new ArgumentOutOfRangeException(nameof(stopOn));
        if (maxLifetime is { } lifetime && lifetime <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(maxLifetime));
        RepeatPolicy = repeatPolicy;
        StopOn = stopOn;
        MaxLifetime = maxLifetime;
    }
}
