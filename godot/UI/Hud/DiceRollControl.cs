using System;
using Godot;
using RollForHonor.Godot.UI.Common;

namespace RollForHonor.Godot.UI.Hud;

/// <summary>A centered d20 control opening the retained roll history on click.</summary>
public partial class DiceRollControl : Control
{
    /// <summary>Raised when the user requests the nested roll history.</summary>
    public event Action Requested;

    private Label _number;
    private bool _hovered;

    /// <inheritdoc />
    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Stop;
        MouseDefaultCursorShape = CursorShape.PointingHand;
        _number = UiLayout.Place(this, UiLayout.Text("—", 27, HonorTheme.Parchment, true), new Rect2(18, 22, 60, 36));
        _number.Name = "Value";
        _number.HorizontalAlignment = HorizontalAlignment.Center;
        _number.VerticalAlignment = VerticalAlignment.Center;
        var caption = UiLayout.Place(this, new RichTextLabel
        {
            Name = "Caption",
            BbcodeEnabled = true,
            MetaUnderlined = true,
            ScrollActive = false,
            Text = "[center][url=roll]LAST ROLL[/url][/center]",
            MouseFilter = MouseFilterEnum.Stop
        }, new Rect2(0, 82, 96, 20));
        caption.AddThemeFontSizeOverride("normal_font_size", 10);
        caption.AddThemeColorOverride("default_color", HonorTheme.Muted);
        caption.MetaClicked += _ => Requested?.Invoke();
        MouseEntered += () => SetHovered(true);
        MouseExited += () => SetHovered(false);
    }

    /// <inheritdoc />
    public override void _Draw()
    {
        Vector2[] vertices = [new(48, 0), new(84, 20), new(84, 60), new(48, 80), new(12, 60), new(12, 20)];
        DrawColoredPolygon(vertices, new Color(HonorTheme.Ink, 0.85f));
        var edge = _hovered ? HonorTheme.Brass : HonorTheme.Border.Lightened(0.15f);

        for (var index = 0; index < vertices.Length; index++)
        {
            DrawLine(vertices[index], vertices[(index + 1) % vertices.Length], edge, 1.5f, true);
        }

        var facet = new Color(edge, 0.35f);
        DrawPolyline([vertices[0], new Vector2(28, 52), new Vector2(68, 52), vertices[0]], facet, 1, true);
        DrawLine(vertices[5], new Vector2(28, 52), facet, 1, true);
        DrawLine(vertices[1], new Vector2(68, 52), facet, 1, true);
        DrawLine(vertices[3], new Vector2(28, 52), facet, 1, true);
        DrawLine(vertices[3], new Vector2(68, 52), facet, 1, true);
    }

    /// <inheritdoc />
    public override void _GuiInput(InputEvent input)
    {
        if (input is InputEventMouseButton { ButtonIndex: MouseButton.Left, Pressed: true })
        {
            AcceptEvent();
            Requested?.Invoke();
        }
    }

    /// <summary>Displays the retained raw d20 result without rolling again.</summary>
    /// <param name="value">The latest actual result, or null before the first roll.</param>
    public void UpdateValue(int? value) => _number.Text = value?.ToString() ?? "—";

    private void SetHovered(bool hovered)
    {
        _hovered = hovered;
        QueueRedraw();
    }
}
