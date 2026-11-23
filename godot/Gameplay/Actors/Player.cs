using Godot;
using RollForHonor.Godot.Gameplay.Input;

namespace RollForHonor.Godot.Gameplay.Actors;

/// <summary>Adapts player controls to shared actor capabilities and samples facing after movement.</summary>
public partial class Player : DynamicEntity
{
    /// <summary>Gets the input controller after actor initialization.</summary>
    public PlayerInput Controls => GetRequiredComponent<PlayerInput>();

    /// <inheritdoc />
    public override void _PhysicsProcess(double delta)
    {
        if (!IsConfigured)
        {
            return;
        }

        base._PhysicsProcess(delta);
        Controls.UpdateCursorFacing();
    }

    /// <inheritdoc />
    public override void _UnhandledInput(InputEvent @event)
    {
        if (!IsConfigured || !Controls.HardwareInput || !@event.IsActionPressed("rfh_primary"))
        {
            return;
        }

        Controls.ShootAtCursor(@event is InputEventMouseButton mouse ? mouse.Position : GetViewport().GetMousePosition());
        GetViewport().SetInputAsHandled();
    }
}
