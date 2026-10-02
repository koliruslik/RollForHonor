using RollForHonor.Domain.Combat.State;

namespace RollForHonor.Domain.Combat.StateChanges;

public sealed record HealthChanged : ICombatStateChange
{
    public CombatantId CombatantId { get; }
    public int PreviousHealth { get; }
    public int CurrentHealth { get; }

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
}
