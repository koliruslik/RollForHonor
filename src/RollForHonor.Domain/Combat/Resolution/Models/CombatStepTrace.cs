using System.Collections.Immutable;

namespace RollForHonor.Domain.Combat.Resolution.Models;

/// <summary>Records one completed step and its nested executions, without wall-clock data.</summary>
public sealed record CombatStepTrace
{
    /// <summary>Gets the diagnostic step name, not a persistence identifier.</summary>
    public string Name { get; }
    /// <summary>Gets the inclusive index of the first dice roll belonging to this step.</summary>
    public int DiceStartIndex { get; }
    /// <summary>Gets the exclusive index after the last dice roll belonging to this step.</summary>
    public int DiceEndIndex { get; }
    /// <summary>Gets completed children in execution order.</summary>
    public ImmutableArray<CombatStepTrace> Children { get; }

    /// <summary>Creates a deterministic execution record.</summary>
    /// <param name="name">Non-empty diagnostic name.</param>
    /// <param name="diceStartIndex">Dice trace length before execution.</param>
    /// <param name="diceEndIndex">Dice trace length after execution.</param>
    /// <param name="children">Nested steps, copied into an immutable array.</param>
    /// <exception cref="ArgumentException">The name is empty or children contain null.</exception>
    /// <exception cref="ArgumentNullException">Children are null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Dice indices are negative or reversed.</exception>
    public CombatStepTrace(string name, int diceStartIndex, int diceEndIndex,
        IEnumerable<CombatStepTrace> children)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentOutOfRangeException.ThrowIfNegative(diceStartIndex);
        ArgumentOutOfRangeException.ThrowIfLessThan(diceEndIndex, diceStartIndex);
        ArgumentNullException.ThrowIfNull(children);
        var items = children.ToImmutableArray();
        if (items.Any(child => child is null))
            throw new ArgumentException("Trace children cannot contain null.", nameof(children));
        Name = name;
        DiceStartIndex = diceStartIndex;
        DiceEndIndex = diceEndIndex;
        Children = items;
    }
}
