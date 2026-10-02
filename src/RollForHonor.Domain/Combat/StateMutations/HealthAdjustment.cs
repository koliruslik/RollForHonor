using RollForHonor.Domain.Combat.State;

namespace RollForHonor.Domain.Combat.StateMutations;

public sealed record HealthAdjustment
{
    public CombatantId CombatantId { get; }

    public int Amount { get; }

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
