using Godot;
using System.Text.RegularExpressions;
using RollForHonor.Godot.UI.Localization;

namespace RollForHonor.Godot.UI.Common;

/// <summary>Writes safe rich text with consistent color, link and affix conventions.</summary>
public static class InspectionText
{
    private static readonly Regex DamageWord = new(@"(?<![\p{L}\p{N}_])(?:damage|шкода|шкоди|шкоді|шкоду|шкодою)(?![\p{L}\p{N}_])", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    /// <summary>Writes an underlined, colored link, optionally marked as an affix contribution.</summary>
    /// <param name="target">The native text control.</param>
    /// <param name="text">Plain text, never interpreted as markup.</param>
    /// <param name="id">The local metadata key handled by the owning control.</param>
    /// <param name="color">The concept's semantic color.</param>
    /// <param name="affix">Whether to use italic styling.</param>
    public static void Link(RichTextLabel target, string text, string id, Color color, bool affix = false)
    {
        text = L.Refresh(text);
        var position = 0;

        foreach (Match match in DamageWord.Matches(text))
        {
            WriteLinked(target, text[position..match.Index], id, color, affix);
            Write(target, match.Value, HonorTheme.Parchment, affix);
            position = match.Index + match.Length;
        }

        WriteLinked(target, text[position..], id, color, affix);
    }

    /// <summary>Writes colored plain text while preserving the surrounding rich-text stack.</summary>
    /// <param name="target">The native text control.</param>
    /// <param name="text">Plain text.</param>
    /// <param name="color">The text color.</param>
    /// <param name="italic">Whether the text represents affix contributions.</param>
    public static void Write(RichTextLabel target, string text, Color color, bool italic = false)
    {
        text = L.Refresh(text);
        var position = 0;

        foreach (Match match in DamageWord.Matches(text))
        {
            WriteRun(target, text[position..match.Index], color, italic);
            WriteRun(target, match.Value, HonorTheme.Parchment, italic);
            position = match.Index + match.Length;
        }

        WriteRun(target, text[position..], color, italic);
    }

    private static void WriteLinked(RichTextLabel target, string text, string id, Color color, bool affix)
    {
        if (text.Length == 0)
        {
            return;
        }

        target.PushMeta(id);
        target.PushUnderline();
        Write(target, text, color, affix);
        target.Pop();
        target.Pop();
    }

    private static void WriteRun(RichTextLabel target, string text, Color color, bool italic)
    {
        target.PushColor(color);

        if (italic)
        {
            target.PushItalics();
        }

        target.AddText(text);

        if (italic)
        {
            target.Pop();
        }

        target.Pop();
    }
}
