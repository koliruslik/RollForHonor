namespace RollForHonor.Domain.Combat.State.Models;

/// <summary>
/// Identifies a combatant across snapshots and state changes.
/// </summary>
public sealed record CombatantId
{
    /// <summary>Gets the underlying identifier value.</summary>
    public Guid Value { get; }

    /// <summary>
    /// Creates a non-empty combatant identifier.
    /// </summary>
    public CombatantId(Guid value)
    {
        if (value == Guid.Empty)
        {
            throw new ArgumentException(
                "A combatant identifier cannot be empty.",
                nameof(value));
        }

        Value = value;
    }

    public override string ToString()
    {
        return Value.ToString("D");
    }
}
