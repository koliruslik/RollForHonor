namespace RollForHonor.Domain.Items.Affixes.Models;

/// <summary>Defines a family-specific tier threshold and its modifier ranges.</summary>
public sealed class AffixTier
{
    /// <summary>Gets the stable tier identifier within its affix definition.</summary>
    public string Id { get; }

    /// <summary>Gets the minimum supplied power needed to make this tier eligible; power is not spent.</summary>
    public int RequiredPower { get; }

    /// <summary>Gets the nonempty set of modifier intervals granted by this tier.</summary>
    public IReadOnlyList<AffixModifierRange> Modifiers { get; }

    /// <summary>Copies one tier's threshold and modifier ranges.</summary>
    /// <param name="id">A nonblank tier identifier.</param>
    /// <param name="requiredPower">A nonnegative eligibility threshold, separate from selection weight.</param>
    /// <param name="modifiers">At least one nonnull modifier interval.</param>
    /// <exception cref="ArgumentNullException">The interval collection is null.</exception>
    /// <exception cref="ArgumentException">The ID is blank or intervals are empty/contain null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Required power is negative.</exception>
    public AffixTier(string id, int requiredPower, IEnumerable<AffixModifierRange> modifiers)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        ArgumentOutOfRangeException.ThrowIfNegative(requiredPower);
        ArgumentNullException.ThrowIfNull(modifiers);

        var entries = modifiers.ToArray();

        if (entries.Length == 0 || entries.Any(entry => entry is null))
        {
            throw new ArgumentException("A tier requires nonnull modifier intervals.", nameof(modifiers));
        }

        Id = id;
        RequiredPower = requiredPower;
        Modifiers = Array.AsReadOnly(entries);
    }
}
