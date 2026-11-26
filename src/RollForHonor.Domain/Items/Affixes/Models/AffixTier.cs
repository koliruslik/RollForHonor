namespace RollForHonor.Domain.Items.Affixes.Models;

/// <summary>Defines a family-specific tier threshold and its modifier ranges.</summary>
public sealed class AffixTier
{
    /// <summary>Gets the stable tier identifier within its affix definition.</summary>
    public string Id { get; }

    /// <summary>Gets the minimum supplied power or effective tier roll needed; eligibility also requires the item-level gate.</summary>
    public int RequiredPower { get; }

    /// <summary>Gets the minimum item level, independent of the tier's roll threshold.</summary>
    public int RequiredItemLevel { get; }

    /// <summary>Gets the nonempty set of modifier intervals granted by this tier.</summary>
    public IReadOnlyList<AffixModifierRange> Modifiers { get; }

    /// <summary>Copies one tier's threshold and modifier ranges.</summary>
    /// <param name="id">A nonblank tier identifier.</param>
    /// <param name="requiredPower">A nonnegative eligibility threshold, separate from selection weight.</param>
    /// <param name="modifiers">At least one nonnull modifier interval.</param>
    /// <param name="requiredItemLevel">The positive item-level gate; existing content defaults to level 1.</param>
    /// <exception cref="ArgumentNullException">The interval collection is null.</exception>
    /// <exception cref="ArgumentException">The ID is blank or intervals are empty/contain null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Required power is negative or the item-level gate is not positive.</exception>
    public AffixTier(string id, int requiredPower, IEnumerable<AffixModifierRange> modifiers, int requiredItemLevel = 1)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        ArgumentOutOfRangeException.ThrowIfNegative(requiredPower);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(requiredItemLevel);
        ArgumentNullException.ThrowIfNull(modifiers);

        var entries = modifiers.ToArray();

        if (entries.Length == 0 || entries.Any(entry => entry is null))
        {
            throw new ArgumentException("A tier requires nonnull modifier intervals.", nameof(modifiers));
        }

        Id = id;
        RequiredPower = requiredPower;
        RequiredItemLevel = requiredItemLevel;
        Modifiers = Array.AsReadOnly(entries);
    }
}
