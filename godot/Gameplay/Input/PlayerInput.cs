using Godot;
using RollForHonor.Godot.Gameplay.Actors;
using RollForHonor.Godot.Gameplay.Commands;
using RollForHonor.Godot.Gameplay.Movement;

namespace RollForHonor.Godot.Gameplay.Input;

/// <summary>Converts player movement, cursor input and clicks into actor intentions.</summary>
public partial class PlayerInput : EntityComponent
{
    /// <inheritdoc />
    public override ActorPhysicsStage PhysicsStage => ActorPhysicsStage.Input;

    /// <summary>Gets or sets whether hardware controls are sampled.</summary>
    [Export]
    public bool HardwareInput { get; set; } = true;

    /// <summary>Gets or sets camera-relative movement axes used when hardware sampling is disabled.</summary>
    public Vector2 MovementIntent { get; set; }

    /// <summary>Gets or sets an optional equipment gate for hardware shooting.</summary>
    public System.Func<bool> CanShoot { get; set; }

    private ActorState _state;

    private MouseFacing _facing;
    private MovementMotor _motor;

    private CommandProcessor _commands;
    private Camera3D _camera;

    /// <inheritdoc />
    public override void OnComponentsInitialized()
    {
        _state = Host.GetRequiredComponent<ActorState>();
        _facing = Host.GetRequiredComponent<MouseFacing>();
        _motor = Host.GetRequiredComponent<MovementMotor>();
        _commands = Host.GetRequiredComponent<CommandProcessor>();
    }

    /// <inheritdoc />
    public override void PhysicsStep(double delta)
    {
        if (!_state.AllowsMovement)
        {
            _motor.StopHorizontalMotion();
            return;
        }

        var intent = HardwareInput
            ? global::Godot.Input.GetVector("arena_left", "arena_right", "arena_forward", "arena_back")
            : MovementIntent;
        intent = intent.LimitLength();

        var right = _camera.GlobalBasis.X;
        var back = _camera.GlobalBasis.Z;
        right.Y = back.Y = 0;

        _motor.MovementIntent = right.Normalized() * intent.X + back.Normalized() * intent.Y;
    }

    /// <summary>Connects the camera used to convert screen input into world intent.</summary>
    /// <param name="camera">The scene's active camera.</param>
    public void Configure(Camera3D camera) => _camera = camera;

    /// <summary>Reads the current cursor and updates aiming after the actor has completed movement.</summary>
    /// <remarks>Called by the player host after its ordered component steps. Disabled hardware input preserves externally supplied aiming.</remarks>
    public void UpdateCursorFacing()
    {
        if (!HardwareInput)
        {
            return;
        }

        _facing.SampleCursor(GetViewport().GetMousePosition());
    }

    /// <summary>Queues a screen-space shot to be sampled at the next physics command stage.</summary>
    /// <param name="screen">The viewport-space cursor location at the time of the click.</param>
    public void ShootAtCursor(Vector2 screen)
    {
        if (CanShoot?.Invoke() != false)
        {
            _commands.Enqueue(new CursorShot(screen, _facing));
        }
    }

    private sealed record CursorShot(Vector2 Screen, MouseFacing Facing) : IEntityCommand
    {
        /// <inheritdoc />
        public void Execute(DynamicEntity actor)
        {
            if (Facing.TryGetAttackPoint(Screen, out var point)
                && new Vector2(point.X - actor.GlobalPosition.X, point.Z - actor.GlobalPosition.Z).Length() > 0.2f)
            {
                new AttackAt(point).Execute(actor);
            }
        }
    }
}
