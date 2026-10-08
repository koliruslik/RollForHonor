namespace RollForHonor.Domain.Combat.State.Models;

/// <summary>
/// Describes health before and after applying one atomic set of adjustments.
/// </summary>
public sealed record HealthProjection
{
    /// <summary>Gets the projected combatant identifier.</summary>
    public CombatantId CombatantId { get; }

    /// <summary>Gets health before the atomic adjustment.</summary>
    public int PreviousHealth { get; }

    /// <summary>Gets health after the atomic adjustment.</summary>
    public int CurrentHealth { get; }

    /// <summary>Gets whether health changed.</summary>
    public bool HasChanged => PreviousHealth != CurrentHealth;

    /// <summary>Gets whether the combatant transitioned from living to defeated.</summary>
    public bool BecameDefeated => PreviousHealth > 0 && CurrentHealth == 0;

    /// <summary>
    /// Creates a projected health transition for a combatant.
    /// </summary>
    /// <param name="combatantId">The projected combatant identifier.</param>
    /// <param name="previousHealth">The non-negative health before projection.</param>
    /// <param name="currentHealth">The non-negative projected health.</param>
    /// <exception cref="ArgumentNullException"><paramref name="combatantId"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Either health value is negative.</exception>
    public HealthProjection(
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

    /// <inheritdoc />
    public override string ToString()
    {
        return $"{CombatantId}: {PreviousHealth} -> {CurrentHealth}";
    }
}
