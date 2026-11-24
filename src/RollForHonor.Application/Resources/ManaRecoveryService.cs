using RollForHonor.Domain.Combat.State.Models;

namespace RollForHonor.Application.Resources;

/// <summary>Converts caller-owned game time into a bounded mana restoration request.</summary>
public sealed class ManaRecoveryService
{
    private readonly IManaRecoveryStateAdapter _state;

    /// <summary>Uses the resource publication port.</summary>
    /// <param name="state">The atomic mana recovery port.</param>
    /// <exception cref="ArgumentNullException">The port is null.</exception>
    public ManaRecoveryService(IManaRecoveryStateAdapter state)
    {
        ArgumentNullException.ThrowIfNull(state);
        _state = state;
    }

    /// <summary>Restores mana for elapsed unpaused game time.</summary>
    /// <param name="owner">The living stat-managed recipient.</param>
    /// <param name="elapsed">Nonnegative elapsed game time.</param>
    /// <param name="perSecond">Nonnegative recovery rate in mana per game second.</param>
    /// <returns>The amount actually restored.</returns>
    /// <exception cref="ArgumentNullException">The owner is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">The interval or rate is negative.</exception>
    /// <exception cref="OverflowException">The request or session version cannot be represented.</exception>
    public decimal Recover(CombatantId owner, TimeSpan elapsed, decimal perSecond)
    {
        ArgumentNullException.ThrowIfNull(owner);
        ArgumentOutOfRangeException.ThrowIfLessThan(elapsed, TimeSpan.Zero);
        ArgumentOutOfRangeException.ThrowIfNegative(perSecond);

        return _state.RestoreMana(owner, checked((decimal)elapsed.Ticks / TimeSpan.TicksPerSecond * perSecond));
    }
}
