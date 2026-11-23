using RollForHonor.Domain.Combat.State.Models;

namespace RollForHonor.Domain.Combat.State;

/// <summary>Tracks manual/automatic targeting and a bounded combat timeout for any participant.</summary>
public sealed class EngagementState
{
    /// <summary>Gets the selected counterpart, or null when nothing is selected.</summary>
    public CombatantId? TargetId { get; private set; }

    /// <summary>Gets whether target selection was explicitly requested by a controller.</summary>
    public bool IsManual { get; private set; }

    /// <summary>Gets the exclusive combat timeout on the session clock.</summary>
    public TimeSpan CombatUntil { get; private set; }

    /// <summary>Selects or clears a manual target independently of combat.</summary>
    /// <param name="target">A participant identity, or null to clear selection.</param>
    public void Select(CombatantId? target)
    {
        TargetId = target;
        IsManual = target is not null;
    }

    /// <summary>Ends combat and clears selection when this participant leaves play.</summary>
    public void Reset()
    {
        TargetId = null;
        IsManual = false;
        CombatUntil = TimeSpan.Zero;
    }

    /// <summary>Records an actual interaction and refreshes automatic selection unless manually locked.</summary>
    /// <param name="counterpart">The other participant.</param>
    /// <param name="now">The current nonnegative session time.</param>
    /// <param name="duration">A positive combat duration.</param>
    /// <exception cref="ArgumentNullException">The counterpart is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Time or duration is invalid.</exception>
    /// <exception cref="OverflowException">The combat deadline exceeds the supported time range.</exception>
    public void Record(CombatantId counterpart, TimeSpan now, TimeSpan duration)
    {
        ArgumentNullException.ThrowIfNull(counterpart);
        ArgumentOutOfRangeException.ThrowIfLessThan(now, TimeSpan.Zero);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(duration, TimeSpan.Zero);
        CombatUntil = now + duration;

        if (!IsManual)
        {
            TargetId = counterpart;
        }
    }

    /// <summary>Reports whether the latest interaction remains within its combat window.</summary>
    /// <param name="now">Current session time.</param>
    /// <returns>True strictly before the timeout.</returns>
    public bool IsInCombat(TimeSpan now) => now < CombatUntil;

    /// <summary>Clears invalid targets and expires automatic selection, retaining living manual selections.</summary>
    /// <param name="now">Current session time.</param>
    /// <param name="isAlive">Queries whether the selected participant remains valid and alive.</param>
    /// <exception cref="ArgumentNullException">The query is null.</exception>
    public void Advance(TimeSpan now, Func<CombatantId, bool> isAlive)
    {
        ArgumentNullException.ThrowIfNull(isAlive);

        if (TargetId is not null && (!isAlive(TargetId) || !IsManual && !IsInCombat(now)))
        {
            TargetId = null;
            IsManual = false;
        }
    }
}
