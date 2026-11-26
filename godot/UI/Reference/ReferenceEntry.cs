using System;
using System.Linq;
using RollForHonor.Godot.UI.Common;
using RollForHonor.Godot.UI.Localization;

namespace RollForHonor.Godot.UI.Reference;

/// <summary>Indexes a captured explanation without using translated display text as its identity.</summary>
/// <param name="Id">The stable reference identity.</param>
/// <param name="Category">The displayed group.</param>
/// <param name="Topic">The captured explanation shared with normal inspection cards.</param>
/// <param name="Table">Optional structured table shown instead of a long article.</param>
/// <param name="CapturedLuck">Nonnegative player Luck captured for a monster reward table.</param>
public sealed record ReferenceEntry(string Id, string Category, InspectionTopic Topic, ReferenceTable Table = null, decimal CapturedLuck = 0)
{
    /// <summary>Gets the captured category in the current interface language.</summary>
    public string Category => L.Refresh(_category);

    private readonly string _category = Category;

    /// <summary>Checks plain title, category, description, fact and related-link text against whitespace-separated search words.</summary>
    /// <param name="query">The optional case-insensitive search phrase.</param>
    /// <returns>True when every nonempty search word appears somewhere in the entry.</returns>
    public bool Matches(string query)
    {
        var text = string.Join(" ", new[] { Id, Category, Topic.Title, Topic.Description }
            .Concat(Topic.Facts
                .Select(fact => fact.Text))
            .Concat(Topic.Links
                .Select(link => link.Text)));

        return (query ?? "").Split((char[])null, StringSplitOptions.RemoveEmptyEntries)
            .All(word => text.Contains(word, StringComparison.OrdinalIgnoreCase));
    }
}
