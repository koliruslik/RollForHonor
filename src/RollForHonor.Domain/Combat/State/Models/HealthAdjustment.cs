using System.Globalization;

namespace RollForHonor.Domain.Combat.State.Models;

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
    /// <param name="combatantId">The combatant whose health is adjusted.</param>
    /// <param name="amount">A non-zero signed delta; positive heals and negative damages.</param>
    /// <exception cref="ArgumentNullException"><paramref name="combatantId"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="amount"/> is zero.</exception>
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

    /// <inheritdoc />
    public override string ToString()
    {
        var amount = Amount.ToString("+0;-0;0", CultureInfo.InvariantCulture);

        return $"{CombatantId}: {amount} health";
    }
}
