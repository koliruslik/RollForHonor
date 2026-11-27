using System;
using System.Globalization;
using System.Linq;
using System.Runtime.CompilerServices;
using Godot;
using RollForHonor.Domain.Combat.Damage.Models;

namespace RollForHonor.Godot.UI.Localization;

/// <summary>Translates presentation text while keeping content identities and combat values unchanged.</summary>
public static class L
{
    private static readonly ConditionalWeakTable<string, Message> Messages = new();

    /// <summary>Looks up a complete source message in Godot's registered translations.</summary>
    /// <param name="source">English source text or a known display name.</param>
    /// <returns>The localized message, or the original text when no translation exists.</returns>
    public static string Text(string source) => Refresh(source);

    /// <summary>Renders captured presentation text in the current locale without changing its retained values.</summary>
    /// <param name="text">A source message or text previously produced by this class.</param>
    /// <returns>The translated text with its original message identity retained.</returns>
    public static string Refresh(string text)
    {
        var message = Messages.TryGetValue(text, out var captured) ? captured : new Message(text, null);
        var rendered = new string(message.Render().AsSpan());

        if (rendered.Length > 0)
        {
            Messages.Add(rendered, message);
        }

        return rendered;
    }

    /// <summary>Translates a message template before inserting captured values.</summary>
    /// <param name="message">The complete interpolated source message with its numeric formats.</param>
    /// <returns>Localized text with invariant numeric formatting and translated enum/display-name arguments.</returns>
    public static string Format(FormattableString message)
    {
        var captured = new Message(message.Format, message.GetArguments().ToArray());
        var rendered = new string(captured.Render().AsSpan());

        if (rendered.Length > 0)
        {
            Messages.Add(rendered, captured);
        }

        return rendered;
    }

    /// <summary>Updates a native label or button and retains the source for subsequent locale changes.</summary>
    /// <param name="control">The native Label or Button.</param>
    /// <param name="text">Source text or a captured localized message.</param>
    public static void SetText(Control control, string text)
    {
        var binding = control.GetNodeOrNull<LocaleBinding>("LocaleText");

        if (binding is null)
        {
            binding = new LocaleBinding { Name = "LocaleText" };
            control.AddChild(binding);
        }

        binding.RefreshText = () =>
        {
            var rendered = Refresh(text);

            if (control is Label label)
            {
                label.Text = rendered;
            }
            else if (control is Button button)
            {
                button.Text = rendered;
            }
        };
        binding.RefreshText();
    }

    /// <summary>Refreshes constructed rich text when Godot announces a locale change.</summary>
    /// <param name="owner">The node owning the callback's lifetime.</param>
    /// <param name="refresh">Rebuilds presentation from retained facts without changing gameplay state.</param>
    public static void OnChanged(Node owner, Action refresh) =>
        owner.AddChild(new LocaleBinding { RefreshText = refresh });

    /// <summary>Formats a weapon damage component without exposing Domain's diagnostic enum names.</summary>
    /// <param name="component">The actual dice, flat bonus and damage category.</param>
    /// <returns>The original damage formula followed by its translated type.</returns>
    public static string Damage(DamageComponent component)
    {
        var flat = component.FlatDamage switch
        {
            > 0 => $" + {component.FlatDamage.ToString(CultureInfo.InvariantCulture)}",
            < 0 => $" - {Math.Abs((long)component.FlatDamage).ToString(CultureInfo.InvariantCulture)}",
            _ => ""
        };

        var formula = component.Multiplier == 1 ? $"{component.Dice}{flat}"
            : $"({component.Dice}{flat}) ×{component.Multiplier.ToString("0.####", CultureInfo.InvariantCulture)}";
        return Format($"{formula} [{component.Type}]");
    }

    private sealed record Message(string Source, object[] Arguments)
    {
        internal string Render()
        {
            var template = TranslationServer.Translate(Source).ToString();

            return Arguments is null ? template : string.Format(CultureInfo.InvariantCulture, template, Arguments
                .Select(value => value switch
                {
                    string text => Refresh(text),
                    Enum or StringName => TranslationServer.Translate(value.ToString()).ToString(),
                    _ => value
                })
                .ToArray());
        }
    }
}
