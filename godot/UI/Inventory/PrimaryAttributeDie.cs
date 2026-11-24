using System;
using Godot;
using RollForHonor.Godot.UI.Common;

namespace RollForHonor.Godot.UI.Inventory;

/// <summary>A faceted d20 attribute tile with pointer and keyboard inspection.</summary>
public partial class PrimaryAttributeDie : Control
{
    /// <summary>Gets or sets the original attribute icon asset key before tree entry.</summary>
    public string IconKey { get; set; }

    /// <summary>Raised when the attribute explanation is requested.</summary>
    public event Action Requested;

    private static readonly Vector2[] Vertices = [new(48, 0), new(90, 24), new(90, 72), new(48, 96), new(6, 72), new(6, 24)];
    private bool _hovered;

    /// <inheritdoc />
    public override void _Ready()
    {
        FocusMode = FocusModeEnum.All;
        MouseFilter = MouseFilterEnum.Stop;
        MouseDefaultCursorShape = CursorShape.PointingHand;
        UiLayout.Place(this, UiLayout.Icon(IconKey), new Rect2(32, 18, 32, 32));
        var value = UiLayout.Place(this, UiLayout.Text("0", 23), new Rect2(16, 52, 64, 30));
        value.Name = "Value";
        value.HorizontalAlignment = HorizontalAlignment.Center;
        MouseEntered += () => Highlight(true);
        MouseExited += () => Highlight(false);
        FocusEntered += QueueRedraw;
        FocusExited += QueueRedraw;
    }

    /// <inheritdoc />
    public override void _Draw()
    {
        var edge = _hovered || HasFocus() ? HonorTheme.Parchment : HonorTheme.Brass;
        DrawColoredPolygon(Vertices, new Color(HonorTheme.Ink, 0.9f));
        DrawPolyline([.. Vertices, Vertices[0]], edge, 1.5f, true);
        var facet = new Color(edge, 0.32f);
        DrawPolyline([Vertices[0], new Vector2(22, 70), new Vector2(74, 70), Vertices[0]], facet, 1, true);
        DrawLine(Vertices[5], new Vector2(22, 70), facet, 1, true);
        DrawLine(Vertices[1], new Vector2(74, 70), facet, 1, true);
        DrawLine(Vertices[3], new Vector2(22, 70), facet, 1, true);
        DrawLine(Vertices[3], new Vector2(74, 70), facet, 1, true);
    }

    /// <inheritdoc />
    public override bool _HasPoint(Vector2 point) => Geometry2D.IsPointInPolygon(point, Vertices);

    /// <inheritdoc />
    public override void _GuiInput(InputEvent input)
    {
        if (input is InputEventMouseButton { ButtonIndex: MouseButton.Left, Pressed: true }
            || input.IsActionPressed("ui_accept"))
        {
            AcceptEvent();
            Requested?.Invoke();
        }
    }

    private void Highlight(bool active)
    {
        _hovered = active;
        QueueRedraw();
    }
}
