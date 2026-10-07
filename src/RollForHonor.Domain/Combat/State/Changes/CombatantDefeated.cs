using RollForHonor.Domain.Combat.State.Models;

namespace RollForHonor.Domain.Combat.State.Changes;

/// <summary>
/// Records that a living combatant reached zero health.
/// </summary>
public sealed record CombatantDefeated : ICombatStateChange
{
    /// <summary>Gets the defeated combatant identifier.</summary>
    public CombatantId CombatantId { get; }

    /// <summary>
    /// Creates a defeat state change for a combatant.
    /// </summary>
    public CombatantDefeated(CombatantId combatantId)
    {
        ArgumentNullException.ThrowIfNull(combatantId);
        CombatantId = combatantId;
    }
}
