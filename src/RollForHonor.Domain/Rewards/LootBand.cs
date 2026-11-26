namespace RollForHonor.Domain.Rewards;

/// <summary>Selects an outcome when a group's modified roll reaches its inclusive threshold.</summary>
public sealed record LootBand
{
    /// <summary>Gets the minimum modified roll that selects this band.</summary>
    public int MinimumRoll { get; }

    /// <summary>Gets the captured gold, equipment or no-reward definition.</summary>
    public LootOutcome Outcome { get; }

    /// <summary>Defines one inclusive threshold and its outcome.</summary>
    /// <param name="minimumRoll">The minimum natural-roll-plus-Luck result.</param>
    /// <param name="outcome">The nonnull reward definition.</param>
    /// <exception cref="ArgumentNullException">The outcome is null.</exception>
    public LootBand(int minimumRoll, LootOutcome outcome)
    {
        ArgumentNullException.ThrowIfNull(outcome);
        MinimumRoll = minimumRoll;
        Outcome = outcome;
    }
}
