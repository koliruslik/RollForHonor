using System;
using Godot;

namespace RollForHonor.Godot.Arena;

/// <summary>Follows a world target with fixed angled top-down orientation and independent zoom.</summary>
public partial class ArenaCamera : Node3D
{
    /// <summary>Gets or sets a viewport-coordinate UI hit check used before world zoom.</summary>
    public Func<Vector2, bool> IsZoomBlocked { get; set; }

    private Node3D _target;
    /// <summary>Gets or sets the actor path relative to this camera rig.</summary>
    [Export]
    public NodePath TargetPath { get; set; } = new("../Player");
    /// <summary>Gets the composed active camera after tree entry.</summary>
    public Camera3D Camera { get; private set; }

    /// <inheritdoc />
    public override void _Ready()
    {
        _target = GetNode<Node3D>(TargetPath);
        Camera = GetNode<Camera3D>("Camera");
        GlobalPosition = _target.GlobalPosition;
        Camera.LookAt(GlobalPosition, Vector3.Up);
    }

    /// <inheritdoc />
    public override void _Process(double delta) =>
        GlobalPosition = GlobalPosition.Lerp(_target.GlobalPosition, (float)(1 - Math.Exp(-10 * delta)));

    /// <inheritdoc />
    public override void _UnhandledInput(InputEvent @event)
    {
        var point = @event is InputEventMouseButton mouse ? mouse.Position : GetViewport().GetMousePosition();

        if (IsZoomBlocked?.Invoke(point) ?? GetViewport().GuiGetHoveredControl() is not null)
        {
            return;
        }

        var closer = @event.IsActionPressed("rfh_zoom_in");
        var farther = @event.IsActionPressed("rfh_zoom_out");

        if (!closer && !farther)
        {
            return;
        }

        Camera.Size = Mathf.Clamp(Camera.Size + (closer ? -2 : 2), 14, 36);
        GetViewport().SetInputAsHandled();
    }
}
