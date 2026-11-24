using RollForHonor.Domain.Combat.State.Models;

namespace RollForHonor.Application.Resources;

/// <summary>Restores mana under the same publication boundary as resource spending.</summary>
public interface IManaRecoveryStateAdapter
{
    /// <summary>Adds mana to a living stat-managed actor, clamped to the current resolved maximum.</summary>
    /// <param name="owner">The registered recipient.</param>
    /// <param name="amount">The nonnegative requested recovery.</param>
    /// <returns>The actual amount restored; zero for full or defeated actors.</returns>
    /// <exception cref="ArgumentNullException">The owner is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">The amount is negative.</exception>
    /// <exception cref="KeyNotFoundException">The actor is not stat-managed.</exception>
    /// <exception cref="OverflowException">The session version cannot advance.</exception>
    decimal RestoreMana(CombatantId owner, decimal amount);
}
