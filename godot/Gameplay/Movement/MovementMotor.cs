using Godot;
using RollForHonor.Godot.Gameplay.Actors;

namespace RollForHonor.Godot.Gameplay.Movement;

/// <summary>Executes world-space movement, braking, gravity and collision without knowing actor modes or controllers.</summary>
public partial class MovementMotor : EntityComponent
{
    /// <inheritdoc />
    public override ActorPhysicsStage PhysicsStage => ActorPhysicsStage.Movement;

    /// <summary>Gets or sets maximum intentional horizontal speed in world units per second.</summary>
    [Export]
    public float Speed { get; set; } = 6;

    /// <summary>Gets or sets horizontal world-space intent, clamped to unit length during physics.</summary>
    /// <remarks>Zero intent requests braking, not an immediate stop.</remarks>
    public Vector3 MovementIntent { get; set; }

    /// <summary>Gets the actual velocity after collision response, in world units per second.</summary>
    public Vector3 Velocity => Host.Velocity;

    /// <summary>Gets whether the last native movement step observed floor contact.</summary>
    public bool IsGrounded => Host.IsOnFloor();

    /// <summary>Gets actual horizontal speed after collision response, in world units per second.</summary>
    public float HorizontalSpeed => new Vector2(Velocity.X, Velocity.Z).Length();

    /// <inheritdoc />
    public override void PhysicsStep(double delta)
    {
        var intent = new Vector3(MovementIntent.X, 0, MovementIntent.Z).LimitLength();
        var desired = intent * Speed;
        var velocity = Host.Velocity;
        var horizontal = new Vector3(velocity.X, 0, velocity.Z);
        var acceleration = intent.IsZeroApprox() ? 24 : 18;
        horizontal = horizontal.MoveToward(desired, acceleration * (float)delta);
        velocity.X = horizontal.X;
        velocity.Z = horizontal.Z;
        velocity.Y = IsGrounded && velocity.Y < 0 ? 0 : velocity.Y - 24 * (float)delta;
        Host.Velocity = velocity;
        Host.MoveAndSlide();
    }

    /// <summary>Immediately removes horizontal velocity and intent while preserving vertical motion and gravity.</summary>
    /// <remarks>The caller chooses when a forced stop is required. This motor does not infer defeat, stun or other actor modes.</remarks>
    public void StopHorizontalMotion()
    {
        MovementIntent = Vector3.Zero;
        Host.Velocity = new Vector3(0, Host.Velocity.Y, 0);
    }
}
