using System.Collections.Immutable;

namespace RollForHonor.Domain.Items.Affixes.Models;

/// <summary>Defines a weighted affix option, its exclusivity family, compatibility and tier progression.</summary>
public sealed class AffixDefinition
{
    /// <summary>Gets the stable affix content identifier.</summary>
    public string Id { get; }

    /// <summary>Gets the exclusivity family; at most one affix from this family may occupy an item.</summary>
    public string FamilyId { get; }

    /// <summary>Gets the positive relative selection weight, independent of power thresholds and numeric ranges.</summary>
    public int SelectionWeight { get; }

    /// <summary>Gets tags which must all be present on a compatible item base; an empty set matches every base.</summary>
    public IReadOnlySet<string> RequiredItemTags { get; }

    /// <summary>Gets copied tiers with unique stable identifiers.</summary>
    public IReadOnlyList<AffixTier> Tiers { get; }

    /// <summary>Copies a reusable affix definition.</summary>
    /// <param name="id">A nonblank stable affix identifier.</param>
    /// <param name="familyId">A nonblank exclusivity family identifier.</param>
    /// <param name="selectionWeight">The positive relative weight for selection.</param>
    /// <param name="tiers">Nonempty, nonnull tiers with unique IDs.</param>
    /// <param name="requiredItemTags">Optional required, nonblank base tags.</param>
    /// <exception cref="ArgumentNullException">The tier collection is null.</exception>
    /// <exception cref="ArgumentException">Identity, tags or tiers are invalid.</exception>
    /// <exception cref="ArgumentOutOfRangeException">The weight is not positive.</exception>
    public AffixDefinition(string id, string familyId, int selectionWeight, IEnumerable<AffixTier> tiers,
        IEnumerable<string>? requiredItemTags = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        ArgumentException.ThrowIfNullOrWhiteSpace(familyId);
        ArgumentOutOfRangeException.ThrowIfLessThan(selectionWeight, 1);
        ArgumentNullException.ThrowIfNull(tiers);

        var entries = tiers.ToArray();
        var tags = (requiredItemTags ?? []).ToImmutableHashSet(StringComparer.Ordinal);

        ValidateContent(entries, tags);

        Id = id;
        FamilyId = familyId;
        SelectionWeight = selectionWeight;
        RequiredItemTags = tags;
        Tiers = Array.AsReadOnly(entries);
    }

    private static void ValidateContent(AffixTier[] tiers, ImmutableHashSet<string> tags)
    {
        if (tiers.Length == 0 || tiers.Any(tier => tier is null))
        {
            throw new ArgumentException("Affixes require nonnull, nonempty tiers.", nameof(tiers));
        }

        var uniqueTierCount = tiers
            .DistinctBy(tier => tier.Id)
            .Count();

        if (uniqueTierCount != tiers.Length || tags.Any(string.IsNullOrWhiteSpace))
        {
            throw new ArgumentException("Affixes require nonblank tags and unique tier IDs.");
        }
    }
}
