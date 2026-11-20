#nullable enable
using System;
using Godot;
using RollForHonor.Application.Combat.Ports;
using RollForHonor.Godot.Gameplay.Actors;
using RollForHonor.Godot.Gameplay.Combat;
using RollForHonor.Godot.Gameplay.Commands;
using RollForHonor.Godot.Gameplay.Movement;

namespace RollForHonor.Godot.Gameplay.AI;

/// <summary>Detects one visible living target, pursues directly and queues projectile attacks within range.</summary>
/// <remarks>No navigation, target memory, prediction or physics integration runs here. Actor mode and Application remain authoritative.</remarks>
public partial class EnemyController : EntityComponent
{
    /// <inheritdoc />
    public override ActorPhysicsStage PhysicsStage => ActorPhysicsStage.Input;

    /// <summary>Gets or sets whether decisions run; disabling clears movement on the next physics step.</summary>
    [Export]
    public bool ThinkingEnabled { get; set; } = true;

    /// <summary>Gets or sets the three-dimensional detection radius in world units.</summary>
    [Export]
    public float DetectionRange { get; set; } = 18;

    /// <summary>Gets or sets the maximum three-dimensional firing distance in world units.</summary>
    [Export]
    public float AttackRange { get; set; } = 7;

    /// <summary>Gets whether the latest decision observed a visible living target in detection range.</summary>
    public bool HasVisibleTarget { get; private set; }

    private ActorState _mode = null!;
    private MovementMotor _motor = null!;
    private CommandProcessor _commands = null!;
    private Node3D _visual = null!;
    private DynamicEntity? _target;
    private ICombatStateAdapter _state = null!;
    private Func<bool> _canAttack = null!;

    /// <inheritdoc />
    public override void OnComponentsInitialized()
    {
        _mode = Host.GetRequiredComponent<ActorState>();
        _motor = Host.GetRequiredComponent<MovementMotor>();
        _commands = Host.GetRequiredComponent<CommandProcessor>();
        _visual = Host.GetNode<Node3D>("Visual");
    }

    /// <inheritdoc />
    public override void PhysicsStep(double delta)
    {
        HasVisibleTarget = false;

        if (!ThinkingEnabled || !_mode.AllowsMovement || !GodotObject.IsInstanceValid(_target) || !_target!.IsInsideTree())
        {
            _motor.StopHorizontalMotion();
            return;
        }

        var targetState = _state.Read(Host.CombatantId, _target.CombatantId).Target;
        var offset = _target.GlobalPosition - Host.GlobalPosition;
        var distance = offset.Length();

        if (targetState.IsDefeated || distance > DetectionRange || !CanSeeTarget())
        {
            _motor.StopHorizontalMotion();
            return;
        }

        HasVisibleTarget = true;
        var horizontal = new Vector3(offset.X, 0, offset.Z);

        if (!horizontal.IsZeroApprox())
        {
            _visual.Rotation = new Vector3(0, Mathf.Atan2(-horizontal.X, -horizontal.Z), 0);
        }

        if (distance > AttackRange)
        {
            _motor.MovementIntent = horizontal.Normalized();
            return;
        }

        _motor.StopHorizontalMotion();

        if (_canAttack())
        {
            _commands.Enqueue(new AttackAt(_target.AimPoint));
        }
    }

    /// <summary>Connects an explicit target, current combat views and an authoritative launch-readiness query.</summary>
    /// <param name="target">The actor to detect and attack.</param>
    /// <param name="state">The current session's combat-state reader.</param>
    /// <param name="canAttack">Checks the configured attack cooldown against the shared game clock.</param>
    /// <exception cref="ArgumentNullException">A required dependency is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Ranges are nonfinite, nonpositive or firing range exceeds detection range.</exception>
    public void Configure(DynamicEntity target, ICombatStateAdapter state, Func<bool> canAttack)
    {
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(canAttack);

        if (!float.IsFinite(DetectionRange) || !float.IsFinite(AttackRange)
            || AttackRange <= 0 || DetectionRange < AttackRange)
        {
            throw new ArgumentOutOfRangeException(nameof(AttackRange), "Require 0 < attack range <= detection range.");
        }

        _target = target;
        _state = state;
        _canAttack = canAttack;
    }

    private bool CanSeeTarget()
    {
        var origin = Host.GlobalPosition + Vector3.Up * ProjectileAttack.ShotHeight;
        var query = PhysicsRayQueryParameters3D.Create(origin, _target!.AimPoint, 22);
        query.Exclude = [Host.GetRid()];
        var hit = Host.GetWorld3D().DirectSpaceState.IntersectRay(query);

        return hit.Count != 0 && hit["collider"].AsGodotObject() == _target;
    }
}
