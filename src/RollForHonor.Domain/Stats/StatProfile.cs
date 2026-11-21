namespace RollForHonor.Domain.Stats;

/// <summary>Owns base attributes and a copied modifier list; current HP and resource balances are separate state.</summary>
public sealed class StatProfile
{
    /// <summary>Gets immutable starting values.</summary>
    public StatBlock BaseValues { get; }

    /// <summary>Gets copied modifier entries; equal sources may contribute multiple entries.</summary>
    public IReadOnlyList<StatModifier> Modifiers { get; }

    /// <summary>Captures a base definition and its active modifiers.</summary>
    /// <param name="baseValues">The immutable base definition.</param>
    /// <param name="modifiers">Optional modifier entries from external sources.</param>
    /// <exception cref="ArgumentNullException">The base definition is null.</exception>
    /// <exception cref="ArgumentException">A modifier entry is null.</exception>
    public StatProfile(StatBlock baseValues, IEnumerable<StatModifier>? modifiers = null)
    {
        ArgumentNullException.ThrowIfNull(baseValues);
        var entries = modifiers?.ToArray() ?? [];

        if (entries.Any(entry => entry is null))
        {
            throw new ArgumentException("Modifiers cannot contain null.", nameof(modifiers));
        }

        BaseValues = baseValues;
        Modifiers = Array.AsReadOnly(entries);
    }
}
