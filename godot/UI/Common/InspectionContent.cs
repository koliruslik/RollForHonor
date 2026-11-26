using Godot;
using System.Linq;
using RollForHonor.Godot.UI.Localization;

namespace RollForHonor.Godot.UI.Common;

/// <summary>Renders the same captured explanation in a floating card or the reference reader.</summary>
public static class InspectionContent
{
    /// <summary>Writes plain facts, description and indexed links without interpreting authored text as markup.</summary>
    /// <param name="body">The destination rich text control.</param>
    /// <param name="topic">The immutable explanation; link metadata uses its zero-based link indices.</param>
    public static void Write(RichTextLabel body, InspectionTopic topic)
    {
        body.Clear();

        foreach (var fact in topic.Facts)
        {
            if (fact.LinkIndex is { } index && index >= 0 && index < topic.Links.Count)
            {
                InspectionText.Link(body, fact.Text, index.ToString(), fact.Color, fact.Affix);
                body.AddText("\n");
            }
            else
            {
                InspectionText.Write(body, fact.Text + "\n", fact.Color, fact.Affix);
            }
        }

        if (topic.Facts.Count > 0 && topic.Description.Length > 0)
        {
            body.AddText("\n");
        }

        body.AddText(topic.Description);

        var related = Enumerable.Range(0, topic.Links.Count)
            .Where(index => !topic.Facts.Any(fact => fact.LinkIndex == index))
            .ToArray();

        if (related.Length == 0)
        {
            return;
        }

        InspectionText.Write(body, L.Text("\n\nRELATED\n"), HonorTheme.Muted);

        foreach (var index in related)
        {
            var link = topic.Links[index];
            InspectionText.Link(body, link.Text, index.ToString(), link.Color);
            body.AddText("\n");
        }
    }
}
