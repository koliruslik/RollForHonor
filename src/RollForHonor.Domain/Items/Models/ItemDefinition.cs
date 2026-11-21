using System.Collections.Immutable;

namespace RollForHonor.Domain.Items.Models;

/// <summary>Defines a reusable item base independently of rarity and rolled affixes.</summary>
public sealed class ItemDefinition
{
    /// <summary>Gets the stable content identifier, preserved by owned instances.</summary>
    public string Id { get; }

    /// <summary>Gets the display name of this base.</summary>
    public string Name { get; }

    /// <summary>Gets case-sensitive tags used by affix compatibility rules, such as weapon or armor.</summary>
    public IReadOnlySet<string> Tags { get; }

    /// <summary>Gets immutable implicit modifiers which do not consume affix slots.</summary>
    public IReadOnlyList<ItemModifier> Modifiers { get; }

    /// <summary>Copies the identity, compatibility tags and implicit values of a base.</summary>
    /// <param name="id">A nonblank stable content identifier.</param>
    /// <param name="name">A nonblank display name.</param>
    /// <param name="tags">Optional nonblank compatibility tags.</param>
    /// <param name="modifiers">Optional implicit modifiers, copied in supplied order.</param>
    /// <exception cref="ArgumentException">An identifier/tag is blank or a modifier is null.</exception>
    public ItemDefinition(string id, string name, IEnumerable<string>? tags = null, IEnumerable<ItemModifier>? modifiers = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        var copiedTags = (tags ?? []).ToImmutableHashSet(StringComparer.Ordinal);
        var copiedModifiers = modifiers?.ToArray() ?? [];

        if (copiedTags.Any(string.IsNullOrWhiteSpace) || copiedModifiers.Any(modifier => modifier is null))
        {
            throw new ArgumentException("Tags must be nonblank and modifiers cannot contain null.");
        }

        Id = id;
        Name = name;
        Tags = copiedTags;
        Modifiers = Array.AsReadOnly(copiedModifiers);
    }
}
