using System;
using Godot;

namespace RollForHonor.Godot.UI.Common;

/// <summary>Creates small native UI elements and scales a 1920 by 1080 design surface to the available viewport.</summary>
public static class UiLayout
{
    /// <summary>Adds a centered design surface which keeps its proportions when the window is resized.</summary>
    /// <param name="root">The root Control covering the viewport.</param>
    /// <param name="fillWidth">Whether to fill the viewport width while retaining a 1080-pixel design height.</param>
    /// <returns>The design-space parent for positioned child controls.</returns>
    public static Control Design(Control root, bool fillWidth = false)
    {
        root.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        root.MouseFilter = Control.MouseFilterEnum.Ignore;
        var design = new Control { Name = "Design", Size = new Vector2(1920, 1080), MouseFilter = Control.MouseFilterEnum.Ignore };
        root.AddChild(design);
        root.Resized += () => Fit(root, design, fillWidth);
        Fit(root, design, fillWidth);

        return design;
    }

    /// <summary>Adds a control at a fixed design-space rectangle.</summary>
    /// <typeparam name="T">The native control type.</typeparam>
    /// <param name="parent">The design-space parent.</param>
    /// <param name="control">The configured child control.</param>
    /// <param name="rect">Its position and size in design pixels.</param>
    /// <returns>The added control.</returns>
    public static T Place<T>(Control parent, T control, Rect2 rect) where T : Control
    {
        control.Position = rect.Position;
        control.Size = rect.Size;
        parent.AddChild(control);

        return control;
    }

    /// <summary>Creates a transparent native label with explicit hierarchy and color.</summary>
    /// <param name="text">The displayed text.</param>
    /// <param name="size">The font size in design pixels.</param>
    /// <param name="color">Optional text color; absent uses parchment.</param>
    /// <param name="heading">Whether to use the serif heading font.</param>
    /// <returns>A label which does not intercept world input.</returns>
    public static Label Text(string text, int size = 20, Color? color = null, bool heading = false)
    {
        var label = new Label
        {
            Text = text,
            MouseFilter = Control.MouseFilterEnum.Ignore,
            ThemeTypeVariation = heading ? "Heading" : "",
            VerticalAlignment = VerticalAlignment.Center
        };
        label.AddThemeFontSizeOverride("font_size", size);
        label.AddThemeColorOverride("font_color", color ?? HonorTheme.Parchment);

        return label;
    }

    /// <summary>Creates an interactive action button with visible keyboard focus.</summary>
    /// <param name="text">The action label.</param>
    /// <param name="pressed">The callback invoked once per press.</param>
    /// <param name="primary">Whether to use the brass primary variant.</param>
    /// <returns>A native focusable Button.</returns>
    public static Button Button(string text, Action pressed, bool primary = false)
    {
        var button = new Button
        {
            Text = text,
            ThemeTypeVariation = primary ? "GoldButton" : "",
            MouseDefaultCursorShape = Control.CursorShape.PointingHand
        };
        button.Pressed += pressed;

        return button;
    }

    /// <summary>Creates a square window-close button using the compact close-control theme.</summary>
    /// <param name="pressed">The callback which dismisses the owning window.</param>
    /// <returns>A focusable 40-by-40 button with a centered close glyph.</returns>
    public static Button CloseButton(Action pressed)
    {
        var button = Button("×", pressed);
        button.Name = "Close";
        button.ThemeTypeVariation = "CloseButton";
        button.CustomMinimumSize = new Vector2(40, 40);

        return button;
    }

    /// <summary>Adds a thin ornamental divider.</summary>
    /// <param name="parent">The design-space parent.</param>
    /// <param name="rect">The divider rectangle.</param>
    public static void Divider(Control parent, Rect2 rect) =>
        Place(parent, new ColorRect { Color = HonorTheme.Border, MouseFilter = Control.MouseFilterEnum.Ignore }, rect);

    /// <summary>Adds a stone-colored background panel.</summary>
    /// <param name="parent">The design-space parent.</param>
    /// <param name="name">The stable node name.</param>
    /// <param name="rect">The panel rectangle.</param>
    /// <returns>The panel control.</returns>
    public static Panel Panel(Control parent, string name, Rect2 rect)
    {
        var panel = new Panel { Name = name };
        panel.AddThemeStyleboxOverride("panel", HonorTheme.Panel(HonorTheme.Ink, HonorTheme.Border));

        return Place(parent, panel, rect);
    }

    /// <summary>Creates an original vector item or navigation icon.</summary>
    /// <param name="key">The asset name without its svg extension.</param>
    /// <returns>A centered aspect-preserving image which does not intercept input.</returns>
    public static TextureRect Icon(string key) => new()
    {
        Texture = GD.Load<Texture2D>($"res://UI/Assets/{key}.svg"),
        ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
        StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
        MouseFilter = Control.MouseFilterEnum.Ignore
    };

    private static void Fit(Control root, Control design, bool fillWidth)
    {
        var available = root.Size;
        var scale = fillWidth ? available.Y / 1080f : Math.Min(available.X / 1920f, available.Y / 1080f);
        design.Size = new Vector2(fillWidth ? available.X / Math.Max(scale, 0.01f) : 1920, 1080);
        design.Scale = Vector2.One * Math.Max(scale, 0.01f);
        design.Position = (available - design.Size * design.Scale) / 2;
    }
}
