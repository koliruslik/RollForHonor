using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

namespace RollForHonor.Godot.UI.Common;

/// <summary>A captured explanation and lazy links to related gameplay concepts.</summary>
public sealed class InspectionTopic
{
    /// <summary>Gets the card heading.</summary>
    public string Title { get; }

    /// <summary>Gets plain text rendered without interpreting content as markup.</summary>
    public string Description { get; }

    /// <summary>Gets the semantic heading and border color.</summary>
    public Color Accent { get; }

    /// <summary>Gets copied links which open further explanation cards.</summary>
    public IReadOnlyList<InspectionLink> Links { get; }

    /// <summary>Gets copied item or character values retaining their semantic formatting.</summary>
    public IReadOnlyList<InspectionFact> Facts { get; }

    /// <summary>Captures one explanation without opening related topics yet.</summary>
    /// <param name="title">The heading.</param>
    /// <param name="description">The plain description or retained item facts.</param>
    /// <param name="accent">The semantic color.</param>
    /// <param name="links">Optional related concepts.</param>
    /// <param name="facts">Optional retained values rendered before the description.</param>
    public InspectionTopic(string title, string description, Color accent, IEnumerable<InspectionLink> links = null,
        IEnumerable<InspectionFact> facts = null)
    {
        Title = title;
        Description = description;
        Accent = accent;
        Links = Array.AsReadOnly((links ?? []).ToArray());
        Facts = Array.AsReadOnly((facts ?? []).ToArray());
    }
}

/// <summary>A named, colored transition to an explanation created only when selected.</summary>
/// <param name="Text">The underlined link label.</param>
/// <param name="Color">The concept's semantic color.</param>
/// <param name="Open">Creates the explanation from immutable captured facts.</param>
public sealed record InspectionLink(string Text, Color Color, Func<InspectionTopic> Open);

/// <summary>A retained value with the same type color and affix marker as its source.</summary>
/// <param name="Text">Plain text for the captured value.</param>
/// <param name="Color">The semantic color.</param>
/// <param name="Affix">Whether the value is italicized as an affix contribution.</param>
public sealed record InspectionFact(string Text, Color Color, bool Affix = false);
