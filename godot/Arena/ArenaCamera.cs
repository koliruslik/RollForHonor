using System;
using Godot;

namespace RollForHonor.Godot.Arena;

/// <summary>Follows a world target with fixed angled top-down orientation and independent zoom.</summary>
public partial class ArenaCamera : Node3D
{
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
        if (@event is not InputEventMouseButton { Pressed: true } mouse) return;
        if (mouse.ButtonIndex is not (MouseButton.WheelUp or MouseButton.WheelDown)) return;
        Camera.Size = Mathf.Clamp(Camera.Size + (mouse.ButtonIndex == MouseButton.WheelUp ? -2 : 2), 14, 36);
        GetViewport().SetInputAsHandled();
    }
}
