using Godot;

namespace RollForHonor.Godot.UI.Common;

/// <summary>Defines the shared ink, brass and parchment styling for the native game interface.</summary>
public static class HonorTheme
{
    /// <summary>The warm metal accent for important controls.</summary>
    public static readonly Color Brass = new("af9977");

    /// <summary>The high-contrast parchment text color.</summary>
    public static readonly Color Parchment = new("d8d5cd");

    /// <summary>The subdued secondary text color.</summary>
    public static readonly Color Muted = new("969791");

    /// <summary>The neutral secondary accent for equipment and navigation.</summary>
    public static readonly Color Secondary = new("c4c5ba");

    /// <summary>The opaque stone-panel background.</summary>
    public static readonly Color Ink = new("151b1e");

    /// <summary>The quiet panel outline.</summary>
    public static readonly Color Border = new("55544c");

    /// <summary>Builds an independent Theme using system font fallbacks without distributing platform fonts.</summary>
    /// <returns>The shared styles and native-control typography for one UI root.</returns>
    public static Theme Create()
    {
        var theme = new Theme
        {
            DefaultFont = new SystemFont { FontNames = ["Segoe UI", "Noto Sans", "DejaVu Sans"] },
            DefaultFontSize = 20
        };
        ConfigureLabels(theme);
        ConfigureButtons(theme);
        ConfigureInputs(theme);

        return theme;
    }

    /// <summary>Creates a rectangular stone panel with a fine metal outline.</summary>
    /// <param name="background">The panel fill.</param>
    /// <param name="border">The outline color.</param>
    /// <param name="padding">The content inset in design pixels.</param>
    /// <returns>A new independent style resource.</returns>
    public static StyleBoxFlat Panel(Color background, Color border, int padding = 18)
    {
        var style = new StyleBoxFlat
        {
            BgColor = background,
            BorderColor = border,
            ContentMarginLeft = padding,
            ContentMarginRight = padding,
            ContentMarginTop = padding,
            ContentMarginBottom = padding,
            ShadowColor = new Color(0, 0, 0, 0.45f),
            ShadowSize = 0
        };
        style.SetBorderWidthAll(2);
        style.AntiAliasing = true;
        style.SetCornerRadiusAll(0);

        return style;
    }

    private static void ConfigureLabels(Theme theme)
    {
        theme.SetColor("font_color", "Label", Parchment);
        theme.SetColor("default_color", "RichTextLabel", Parchment);
        theme.SetFont("normal_font", "RichTextLabel", theme.DefaultFont);
        theme.SetFont("italics_font", "RichTextLabel", new SystemFont
        {
            FontNames = ["Segoe UI", "Noto Sans", "DejaVu Sans"],
            FontItalic = true
        });
        theme.SetTypeVariation("Heading", "Label");
        theme.SetFont("font", "Heading", new SystemFont { FontNames = ["Georgia", "Liberation Serif", "Noto Serif"] });
        theme.SetStylebox("panel", "PanelContainer", Panel(Ink, Border));
        theme.SetStylebox("panel", "TooltipPanel", Panel(Ink, Brass));
    }

    private static void ConfigureButtons(Theme theme)
    {
        theme.SetColor("font_color", "Button", Parchment);
        theme.SetColor("font_hover_color", "Button", new Color("f1efe8"));
        theme.SetColor("font_focus_color", "Button", new Color("f1efe8"));
        theme.SetColor("font_disabled_color", "Button", Muted);
        theme.SetStylebox("normal", "Button", Panel(new Color("222526"), Border, 12));
        theme.SetStylebox("hover", "Button", Panel(new Color("303433"), Brass, 12));
        theme.SetStylebox("pressed", "Button", Panel(new Color("393e3c"), Brass, 12));
        theme.SetStylebox("disabled", "Button", Panel(new Color("11191b"), new Color("303833"), 12));
        theme.SetStylebox("focus", "Button", FocusStyle());
        theme.SetTypeVariation("GoldButton", "Button");
        theme.SetStylebox("normal", "GoldButton", Panel(new Color("3b3a32"), Brass, 12));
        theme.SetStylebox("hover", "GoldButton", Panel(new Color("514e40"), new Color("c8baa0"), 12));
        theme.SetStylebox("pressed", "GoldButton", Panel(new Color("454338"), Brass, 12));
        ConfigureCloseButtons(theme);
    }

    private static void ConfigureCloseButtons(Theme theme)
    {
        theme.SetTypeVariation("CloseButton", "Button");
        theme.SetFontSize("font_size", "CloseButton", 20);

        foreach (var state in new[] { "normal", "hover", "pressed", "disabled" })
        {
            var style = (StyleBoxFlat)theme.GetStylebox(state, "Button").Duplicate();
            style.ContentMarginLeft = style.ContentMarginRight = 4;
            style.ContentMarginTop = style.ContentMarginBottom = 4;
            theme.SetStylebox(state, "CloseButton", style);
        }
    }

    private static void ConfigureInputs(Theme theme)
    {
        theme.SetStylebox("normal", "OptionButton", Panel(new Color("222526"), Border, 12));
        theme.SetStylebox("hover", "OptionButton", Panel(new Color("303433"), Brass, 12));
        theme.SetStylebox("panel", "PopupMenu", Panel(Ink, Brass));
        theme.SetColor("font_color", "OptionButton", Parchment);
        var track = Panel(new Color("293735"), Border, 0);
        track.ContentMarginTop = track.ContentMarginBottom = 3;
        track.ShadowSize = 0;
        var fill = Panel(Brass, Brass, 0);
        fill.ContentMarginTop = fill.ContentMarginBottom = 3;
        fill.ShadowSize = 0;
        theme.SetStylebox("slider", "HSlider", track);
        theme.SetStylebox("grabber_area", "HSlider", fill);
        theme.SetIcon("grabber", "HSlider", GD.Load<Texture2D>("res://UI/Assets/slider-knob.svg"));
        theme.SetIcon("grabber_highlight", "HSlider", GD.Load<Texture2D>("res://UI/Assets/slider-knob.svg"));
    }

    private static StyleBoxFlat FocusStyle()
    {
        var style = Panel(new Color(0, 0, 0, 0), Brass, 0);
        style.ShadowSize = 0;
        style.SetBorderWidthAll(2);

        return style;
    }
}
