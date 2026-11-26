using System.Collections.Immutable;
using RollForHonor.Domain.Stats;

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

    /// <summary>Gets intrinsic weapon properties, or null for a nonweapon base.</summary>
    public WeaponProperties? Weapon { get; }

    /// <summary>Gets the inventory rectangle shared by all instances of this base.</summary>
    public ItemFootprint Footprint { get; }

    /// <summary>Gets immutable character requirements and release eligibility.</summary>
    public EquipmentRequirement Requirements { get; }

    /// <summary>Copies the identity, compatibility tags and implicit values of a base.</summary>
    /// <param name="id">A nonblank stable content identifier.</param>
    /// <param name="name">A nonblank display name.</param>
    /// <param name="tags">Optional nonblank compatibility tags.</param>
    /// <param name="modifiers">Optional implicit modifiers, copied in supplied order.</param>
    /// <param name="weapon">Optional intrinsic weapon timing, cost and damage.</param>
    /// <param name="footprint">Optional inventory dimensions; absent uses one cell.</param>
    /// <param name="requirements">Optional level/allocated-attribute requirements; omitted is unrestricted.</param>
    /// <exception cref="ArgumentException">An identifier/tag is blank, a modifier is null or equipment requirements are invalid.</exception>
    public ItemDefinition(string id, string name, IEnumerable<string>? tags = null, IEnumerable<ItemModifier>? modifiers = null,
        WeaponProperties? weapon = null, ItemFootprint? footprint = null, EquipmentRequirement? requirements = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        var copiedTags = (tags ?? []).ToImmutableHashSet(StringComparer.Ordinal);
        var copiedModifiers = modifiers?.ToArray() ?? [];

        if (copiedTags.Any(string.IsNullOrWhiteSpace) || copiedModifiers.Any(modifier => modifier is null))
        {
            throw new ArgumentException("Tags must be nonblank and modifiers cannot contain null.");
        }

        Requirements = requirements ?? new();
        if (Requirements.Level is < 0 or > 10 || Requirements.Value < 0
            || Requirements.Attribute is not (null or StatType.Strength or StatType.Dexterity or StatType.Intelligence)
            || Requirements.Attribute is null && Requirements.Value != 0)
        {
            throw new ArgumentException("Equipment requirements need level 0–10 and a nonnegative STR, DEX or INT threshold.", nameof(requirements));
        }

        Id = id;
        Name = name;
        Tags = copiedTags;
        Modifiers = Array.AsReadOnly(copiedModifiers);
        Weapon = weapon;
        Footprint = footprint ?? ItemFootprint.SingleCell;
    }
}
