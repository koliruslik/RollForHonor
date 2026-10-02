using RollForHonor.Domain.Combat.State;
using RollForHonor.Domain.Combat.StateMutations;

namespace RollForHonor.Domain.Combat.Services;

public sealed class HealthStateProjector : IHealthStateProjector
{
    public HealthProjection Project(
        CombatantSnapshot combatant,
        IReadOnlyList<HealthAdjustment> adjustments)
    {
        ArgumentNullException.ThrowIfNull(combatant);
        ArgumentNullException.ThrowIfNull(adjustments);

        if (adjustments.Any(adjustment => adjustment is null))
        {
            throw new ArgumentException(
                "Health adjustments cannot contain null values.",
                nameof(adjustments));
        }

        if (adjustments.Any(adjustment => adjustment.CombatantId != combatant.Id))
        {
            throw new ArgumentException(
                "Every health adjustment must target the projected combatant.",
                nameof(adjustments));
        }

        var totalAdjustment = adjustments.Sum(
            adjustment => (long)adjustment.Amount);

        var adjustedHealth = combatant.Health + totalAdjustment;
        var currentHealth = (int)Math.Clamp(
            adjustedHealth,
            0L,
            combatant.MaxHealth);

        return new HealthProjection(
            combatant.Id,
            combatant.Health,
            currentHealth);
    }
}
