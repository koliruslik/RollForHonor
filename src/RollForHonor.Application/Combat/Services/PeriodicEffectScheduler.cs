using RollForHonor.Application.Combat.Models;
using RollForHonor.Application.Combat.Ports;
using RollForHonor.Domain.Combat.Effects.Models;
using RollForHonor.Domain.Combat.Resolution.Models;
using RollForHonor.Domain.Combat.Resolution.Services;
using RollForHonor.Domain.Combat.State.Changes;
using RollForHonor.Domain.Combat.State.Models;
using RollForHonor.Domain.Combat.State.Services;
using RollForHonor.Domain.Dice.Models;

namespace RollForHonor.Application.Combat.Services;

/// <summary>Schedules independently timed effect instances on a host-supplied monotonic game clock.</summary>
public sealed class PeriodicEffectScheduler
{
    private readonly ICombatStateAdapter _state;
    private readonly PeriodicEffectService _ticks;
    private readonly Dictionary<EffectInstanceId, ScheduledEffect> _active = [];
    private readonly HashSet<Guid> _observed = [];
    private TimeSpan _time;

    /// <summary>Creates a session scheduler without owning a wall clock or background thread.</summary>
    /// <param name="state">The authoritative state adapter used by ticks and expiry.</param>
    /// <param name="ticks">The independently committed periodic calculation service.</param>
    /// <exception cref="ArgumentNullException">A collaborator is null.</exception>
    public PeriodicEffectScheduler(ICombatStateAdapter state, PeriodicEffectService ticks)
    {
        _state = state ?? throw new ArgumentNullException(nameof(state));
        _ticks = ticks ?? throw new ArgumentNullException(nameof(ticks));
    }

    /// <summary>Registers automatic effects from an already committed resolution, ignoring duplicate notifications.</summary>
    /// <param name="resolution">A successfully committed impact; rejected proposals must never be supplied.</param>
    /// <param name="appliedAt">Game time when the impact committed, no earlier than the last advancement.</param>
    /// <exception cref="ArgumentNullException">The resolution is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Application time precedes the scheduler clock.</exception>
    public void Observe(CombatResolution resolution, TimeSpan appliedAt)
    {
        ArgumentNullException.ThrowIfNull(resolution);
        ArgumentOutOfRangeException.ThrowIfLessThan(appliedAt, _time);

        if (!_observed.Add(resolution.ResolutionId))
        {
            return;
        }

        foreach (var change in resolution.Changes)
        {
            if (change is EffectDurationRefreshed refresh
                && _active.TryGetValue(refresh.EffectId, out var active) && active.Target == refresh.CombatantId)
            {
                active.ExpiresAt = appliedAt + active.Duration;
            }
            else if (change is EffectApplied applied
                && applied.Effect.Definition is PeriodicDamageEffectDefinition
                { TickInterval: { } interval, Duration: { } duration }
                && !_active.ContainsKey(applied.Effect.Id))
            {
                _active.Add(applied.Effect.Id, new(applied.CombatantId, appliedAt + interval,
                    interval, duration, appliedAt + duration));
            }
        }
    }

    /// <summary>Consumes each due slot once, in chronological order, and removes expired or defeated-recipient statuses.</summary>
    /// <param name="now">Current game time; equal times are safe and paused hosts keep this unchanged.</param>
    /// <returns>Every attempted damage tick, including rejected or conflicted attempts; no damage attempt is retried.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Time moves backwards.</exception>
    public IReadOnlyList<PeriodicTickResult> Advance(TimeSpan now)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(now, _time);
        _time = now;
        var results = new List<PeriodicTickResult>();

        foreach (var entry in _active.ToArray())
        {
            var actor = _state.Read(entry.Value.Target, entry.Value.Target).Target;

            if (!actor.Effects.Any(effect => effect.Id == entry.Key))
            {
                _active.Remove(entry.Key);
            }
            else if (actor.IsDefeated)
            {
                entry.Value.Stopped = true;
            }
        }

        while (true)
        {
            var due = _active
                .Where(entry => !entry.Value.Stopped && entry.Value.Next <= now && entry.Value.Next <= entry.Value.ExpiresAt)
                .OrderBy(entry => entry.Value.Next)
                .FirstOrDefault();

            if (due.Key is null)
            {
                break;
            }

            var result = _ticks.Tick(due.Value.Target, due.Key);
            results.Add(result);
            due.Value.Next += due.Value.Interval;

            if (_state.Read(due.Value.Target, due.Value.Target).Target.IsDefeated)
            {
                foreach (var pending in _active.Values.Where(value => value.Target == due.Value.Target))
                {
                    pending.Stopped = true;
                }
            }
        }

        foreach (var entry in _active.Where(entry => entry.Value.Stopped || entry.Value.ExpiresAt <= now).ToArray())
        {
            if (Expire(entry.Value.Target, entry.Key))
            {
                _active.Remove(entry.Key);
            }
        }

        return results;
    }

    private bool Expire(CombatantId target, EffectInstanceId id)
    {
        var read = _state.Read(target, target);
        var effect = read.Target.Effects.SingleOrDefault(value => value.Id == id);

        if (effect is null)
        {
            return true;
        }

        var request = new CombatRequest(Guid.NewGuid(), effect, read.Target, read.Version);
        var context = new CombatContext(request) { StateChanges = [new EffectRemoved(target, id)] };
        var resolution = new CombatResolutionBuilder(new HealthStateProjector()).Build(context, new DiceTrace([]));
        return _state.TryCommit(resolution) == CombatCommitStatus.Applied;
    }

    private sealed class ScheduledEffect(CombatantId target, TimeSpan next, TimeSpan interval, TimeSpan duration, TimeSpan expiresAt)
    {
        internal CombatantId Target { get; } = target;
        internal TimeSpan Next { get; set; } = next;
        internal TimeSpan Interval { get; } = interval;
        internal TimeSpan Duration { get; } = duration;
        internal TimeSpan ExpiresAt { get; set; } = expiresAt;
        internal bool Stopped { get; set; }
    }
}
