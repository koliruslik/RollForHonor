using Godot;
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
            InspectionText.Write(body, fact.Text + "\n", fact.Color, fact.Affix);
        }

        if (topic.Facts.Count > 0 && topic.Description.Length > 0)
        {
            body.AddText("\n");
        }

        body.AddText(topic.Description);

        if (topic.Links.Count == 0)
        {
            return;
        }

        InspectionText.Write(body, L.Text("\n\nRELATED\n"), HonorTheme.Muted);

        for (var index = 0; index < topic.Links.Count; index++)
        {
            var link = topic.Links[index];
            InspectionText.Link(body, link.Text, index.ToString(), link.Color);
            body.AddText("\n");
        }
    }
}
