namespace RollForHonor.Domain.Combat.State;

public sealed record HealthProjection
{
    public CombatantId CombatantId { get; }

    public int PreviousHealth { get; }

    public int CurrentHealth { get; }

    public bool HasChanged => PreviousHealth != CurrentHealth;

    public bool BecameDefeated => PreviousHealth > 0 && CurrentHealth == 0;

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
}
