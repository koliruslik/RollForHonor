using RollForHonor.Domain.Combat.State.Models;

namespace RollForHonor.Application.Resources;

/// <summary>Publishes capped passive healing without reviving defeated actors.</summary>
public interface IHealthRecoveryStateAdapter
{
    /// <summary>Restores whole HP to a living actor under the shared session lock.</summary>
    /// <param name="owner">The registered actor.</param>
    /// <param name="amount">Nonnegative whole HP requested.</param>
    /// <returns>The actual restored HP, or zero for full or defeated actors.</returns>
    /// <exception cref="ArgumentNullException">The owner is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">The amount is negative.</exception>
    /// <exception cref="KeyNotFoundException">The actor is absent.</exception>
    /// <exception cref="OverflowException">The session version cannot advance.</exception>
    int RestoreHealth(CombatantId owner, int amount);
}
