using RollForHonor.Domain.Combat.State.Models;

namespace RollForHonor.Domain.Combat.State.Changes;

/// <summary>
/// Records the canonical health transition of a combatant.
/// </summary>
public sealed record HealthChanged : ICombatStateChange
{
    /// <summary>Gets the affected combatant identifier.</summary>
    public CombatantId CombatantId { get; }

    /// <summary>Gets health before the committed transition.</summary>
    public int PreviousHealth { get; }

    /// <summary>Gets health after the committed transition.</summary>
    public int CurrentHealth { get; }

    /// <summary>
    /// Creates a validated health state change.
    /// </summary>
    public HealthChanged(
        CombatantId combatantId,
        int previousHealth,
        int currentHealth)
    {
        ArgumentNullException.ThrowIfNull(combatantId);
        ArgumentOutOfRangeException.ThrowIfNegative(previousHealth);
        ArgumentOutOfRangeException.ThrowIfNegative(currentHealth);

        CombatantId = combatantId;
        PreviousHealth = previousHealth;
        CurrentHealth = currentHealth;
    }

    public override string ToString()
    {
        return $"{CombatantId}: {PreviousHealth} -> {CurrentHealth}";
    }
}
