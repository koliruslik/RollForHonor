using RollForHonor.Domain.Combat.State;

namespace RollForHonor.Domain.Combat.StateMutations;

/// <summary>
/// Proposes a signed health delta within an atomic resolution.
/// </summary>
public sealed record HealthAdjustment
{
    /// <summary>Gets the combatant whose health is adjusted.</summary>
    public CombatantId CombatantId { get; }

    /// <summary>Gets the signed health delta; positive heals and negative damages.</summary>
    public int Amount { get; }

    /// <summary>
    /// Creates a non-zero health adjustment for a combatant.
    /// </summary>
    public HealthAdjustment(CombatantId combatantId, int amount)
    {
        ArgumentNullException.ThrowIfNull(combatantId);

        if (amount == 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(amount),
                "A health adjustment cannot be zero.");
        }

        CombatantId = combatantId;
        Amount = amount;
    }
}
