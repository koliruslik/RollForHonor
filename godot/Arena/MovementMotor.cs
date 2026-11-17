using System;
using Godot;

namespace RollForHonor.Godot.Arena;

/// <summary>Identifies the current locomotion state independently of mouse-facing direction.</summary>
public enum LocomotionState
{
    /// <summary>The grounded actor has stopped.</summary>
    Idle,
    /// <summary>The grounded actor is moving.</summary>
    Moving,
    /// <summary>The actor is airborne under gravity.</summary>
    Falling,
    /// <summary>Combat state blocks intentional movement.</summary>
    Defeated
}

/// <summary>Applies camera-relative intent, acceleration, braking and native character collision with explicit locomotion transitions.</summary>
public partial class MovementMotor : Node
{
    private CharacterBody3D _body;
    /// <summary>Gets the last observed locomotion state.</summary>
    public LocomotionState State { get; private set; } = LocomotionState.Idle;
    /// <summary>Raised only when a locomotion transition occurs.</summary>
    public event Action<LocomotionState> StateChanged;
    /// <summary>Gets or sets maximum horizontal speed in world units per second.</summary>
    [Export]
    public float Speed { get; set; } = 6;

    /// <inheritdoc />
    public override void _Ready() => _body = GetParent<CharacterBody3D>();

    /// <summary>Advances one physics step without reading hardware input or changing facing.</summary>
    /// <param name="intent">Camera-relative axes, clamped to unit length to normalize diagonal motion.</param>
    /// <param name="camera">The fixed-orientation world camera.</param>
    /// <param name="delta">Elapsed game seconds.</param>
    /// <param name="defeated">Whether live combat state forbids movement.</param>
    public void Step(Vector2 intent, Camera3D camera, double delta, bool defeated)
    {
        intent = intent.LimitLength();
        var right = camera.GlobalBasis.X;
        var back = camera.GlobalBasis.Z;
        right.Y = back.Y = 0;
        var desired = (right.Normalized() * intent.X + back.Normalized() * intent.Y) * Speed;
        var velocity = _body.Velocity;
        var horizontal = new Vector3(velocity.X, 0, velocity.Z);
        horizontal = defeated ? Vector3.Zero : horizontal.MoveToward(desired, (intent.IsZeroApprox() ? 24 : 18) * (float)delta);
        velocity.X = horizontal.X;
        velocity.Z = horizontal.Z;
        velocity.Y = _body.IsOnFloor() && velocity.Y < 0 ? 0 : velocity.Y - 24 * (float)delta;
        _body.Velocity = velocity;
        _body.MoveAndSlide();
        var next = defeated ? LocomotionState.Defeated : !_body.IsOnFloor() ? LocomotionState.Falling
            : new Vector2(_body.Velocity.X, _body.Velocity.Z).Length() > 0.05f ? LocomotionState.Moving : LocomotionState.Idle;
        if (next == State) return;
        State = next;
        StateChanged?.Invoke(next);
    }
}
