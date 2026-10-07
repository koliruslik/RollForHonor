namespace RollForHonor.Domain.Combat.Attacks.Models;

/// <summary>
/// Defines natural d20 ranges that lower or raise an attack outcome by one degree.
/// </summary>
public sealed record NaturalOutcomeShiftRules
{
    /// <summary>Gets the highest natural roll that lowers the outcome.</summary>
    public int DowngradeMaximum { get; }

    /// <summary>Gets the lowest natural roll that raises the outcome.</summary>
    public int UpgradeMinimum { get; }

    /// <summary>
    /// Gets the standard rules where natural 1 lowers and natural 20 raises the outcome.
    /// </summary>
    public static NaturalOutcomeShiftRules Standard { get; } = new(1, 20);

    /// <summary>
    /// Creates validated natural-roll outcome shift rules.
    /// </summary>
    public NaturalOutcomeShiftRules(
        int downgradeMaximum,
        int upgradeMinimum)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(downgradeMaximum, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(downgradeMaximum, 20);
        ArgumentOutOfRangeException.ThrowIfLessThan(upgradeMinimum, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(upgradeMinimum, 20);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(
            downgradeMaximum,
            upgradeMinimum);

        DowngradeMaximum = downgradeMaximum;
        UpgradeMinimum = upgradeMinimum;
    }
}
