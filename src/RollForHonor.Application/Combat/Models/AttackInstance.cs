using RollForHonor.Application.Combat.Services;
using RollForHonor.Domain.Combat.Attacks.Models;
using RollForHonor.Domain.Combat.State.Models;

namespace RollForHonor.Application.Combat.Models;

/// <summary>Binds one accepted attack to its source, contact history and observed lifetime.</summary>
/// <remarks>
/// Source statistics are refreshed per impact. The host supplies physical contacts and animation
/// completion, and calls UpdateLifetime to observe expiry without a contact. No timer, movement,
/// collision detection, automatic retry or resource deduction runs here. Contact admission is
/// reserved before calculation and is not rolled back on rejection, conflict or exception.
/// Ending the instance blocks later admissions; it does not cancel already admitted impacts.
/// </remarks>
public sealed class AttackInstance
{
    private readonly CombatImpactService _impacts;
    private readonly Func<TimeSpan> _now;
    private readonly object _gate = new();
    private readonly HashSet<CombatantId> _contacted = [];
    private AttackEndReason? _endReason;

    /// <summary>Gets the identity shared by the instance's independently resolved impacts.</summary>
    public Guid Id { get; } = Guid.NewGuid();
    /// <summary>Gets the source identity read afresh at each admitted impact.</summary>
    public CombatantId SourceId { get; }
    /// <summary>Gets immutable base attack rules and damage formulas.</summary>
    public AttackPayload Attack { get; }
    /// <summary>Gets configured contact admission and termination rules.</summary>
    public AttackExecutionRules ExecutionRules { get; }
    /// <summary>Gets the launch time on the host's monotonic game timeline.</summary>
    public TimeSpan StartedAt { get; }
    /// <summary>Gets the optional fixed game-time deadline; reading does not consult the clock.</summary>
    public TimeSpan? ExpiresAt { get; }
    /// <summary>Gets the last observed end reason; reading never advances time or changes state.</summary>
    public AttackEndReason? EndReason
    {
        get { lock (_gate) return _endReason; }
    }
    /// <summary>Gets whether the attack has not ended as of its last contact or lifetime update.</summary>
    public bool IsActive => EndReason is null;

    /// <summary>Creates an execution context; normal callers obtain it from accepted launch.</summary>
    /// <param name="sourceId">The source identity.</param>
    /// <param name="attack">The immutable impact payload.</param>
    /// <param name="impacts">The per-impact Application use case.</param>
    /// <param name="executionRules">Optional rules, preserving unrestricted impacts when absent.</param>
    /// <param name="now">Monotonic game clock; required for timed attacks.</param>
    /// <param name="startedAt">Already observed launch time, avoiding an additional clock read.</param>
    /// <exception cref="ArgumentNullException">A required dependency is null.</exception>
    /// <exception cref="OverflowException">The expiry deadline exceeds the supported game-time range.</exception>
    internal AttackInstance(CombatantId sourceId, AttackPayload attack, CombatImpactService impacts,
        AttackExecutionRules? executionRules = null, Func<TimeSpan>? now = null, TimeSpan? startedAt = null)
    {
        ArgumentNullException.ThrowIfNull(sourceId);
        ArgumentNullException.ThrowIfNull(attack);
        ArgumentNullException.ThrowIfNull(impacts);
        SourceId = sourceId;
        Attack = attack;
        _impacts = impacts;
        ExecutionRules = executionRules ?? new AttackExecutionRules();
        if (ExecutionRules.MaxLifetime is not null) ArgumentNullException.ThrowIfNull(now);
        _now = now ?? (() => TimeSpan.Zero);
        StartedAt = startedAt ?? _now();
        ExpiresAt = ExecutionRules.MaxLifetime is { } lifetime ? StartedAt + lifetime : null;
    }

    /// <summary>Calculates a contact that the caller expects to be eligible.</summary>
    /// <param name="targetId">The contacted target identity.</param>
    /// <returns>The calculated Domain outcome and separate commit status.</returns>
    /// <exception cref="ArgumentNullException">Target is null.</exception>
    /// <exception cref="InvalidOperationException">Execution rules reject the contact; use ResolveContact to inspect that status.</exception>
    /// <remarks>Respects the same admission rules as ResolveContact; blocked contacts never roll dice.</remarks>
    public CombatImpactResult Resolve(CombatantId targetId)
    {
        var contact = ResolveContact(targetId);
        return contact.Impact ?? throw new InvalidOperationException($"Attack contact was ignored: {contact.Status}.");
    }

    /// <summary>Admits or ignores one host-reported target contact, calculating at most one impact.</summary>
    /// <param name="targetId">The target identity supplied by the host's collision mapping.</param>
    /// <returns>Admission status, optional impact and observed end reason.</returns>
    /// <exception cref="ArgumentNullException">Target is null.</exception>
    /// <remarks>
    /// OncePerTarget consumes the contact even for Evaded, Domain rejection, commit conflict or
    /// exception. EveryContact treats each call as a distinct host contact. No physical-event
    /// identity or automatic retry is introduced. Calculation/adapter exceptions propagate.
    /// </remarks>
    public AttackContactResult ResolveContact(CombatantId targetId)
    {
        ArgumentNullException.ThrowIfNull(targetId);
        lock (_gate)
        {
            RefreshLifetime();
            if (_endReason is not null) return new(AttackContactStatus.AttackEnded, null, _endReason);
            if (ExecutionRules.RepeatPolicy == AttackRepeatPolicy.OncePerTarget && !_contacted.Add(targetId))
                return new(AttackContactStatus.TargetAlreadyContacted, null, null);
            if (ExecutionRules.StopOn.HasFlag(AttackStopCondition.CombatantContact))
                _endReason = AttackEndReason.CombatantContact;
        }
        return new(AttackContactStatus.Processed, _impacts.Resolve(this, targetId), EndReason);
    }

    /// <summary>Handles one host-reported non-combatant obstacle contact without combat calculation.</summary>
    /// <returns>Contact status and optional termination reason; Impact is always null.</returns>
    public AttackContactResult ContactEnvironment()
    {
        lock (_gate)
        {
            RefreshLifetime();
            if (_endReason is not null) return new(AttackContactStatus.AttackEnded, null, _endReason);
            if (ExecutionRules.StopOn.HasFlag(AttackStopCondition.EnvironmentContact))
                _endReason = AttackEndReason.EnvironmentContact;
            return new(AttackContactStatus.Processed, null, _endReason);
        }
    }

    /// <summary>Observes the game clock and marks an expired attack ended without calculating an impact.</summary>
    /// <returns>True when still active after observing the deadline; false when ended.</returns>
    public bool UpdateLifetime()
    {
        lock (_gate)
        {
            RefreshLifetime();
            return _endReason is null;
        }
    }

    /// <summary>Ends the instance, for example when its host animation finishes.</summary>
    /// <param name="reason">The reported ending condition; defaults to explicit host cancellation.</param>
    /// <returns>True for the first end transition; false when already ended.</returns>
    /// <exception cref="ArgumentOutOfRangeException">The reason is unknown.</exception>
    public bool End(AttackEndReason reason = AttackEndReason.Explicit)
    {
        if (!Enum.IsDefined(reason)) throw new ArgumentOutOfRangeException(nameof(reason));
        lock (_gate)
        {
            if (_endReason is not null) return false;
            _endReason = reason;
            return true;
        }
    }

    private void RefreshLifetime()
    {
        if (_endReason is null && ExpiresAt is { } deadline && _now() >= deadline)
            _endReason = AttackEndReason.LifetimeExpired;
    }
}
