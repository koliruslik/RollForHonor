#nullable enable
using System;
using System.Collections.Generic;
using Godot;
using RollForHonor.Application.Combat.Models;
using RollForHonor.Domain.Combat.Attacks.Models;
using RollForHonor.Domain.Combat.Damage.Models;
using RollForHonor.Domain.Combat.State.Models;
using RollForHonor.Godot.CombatDemo;

namespace RollForHonor.Godot.Arena;

/// <summary>Coordinates input commands, motor, facing and accepted attack delivery without owning combat rules.</summary>
public partial class ArenaPlayer : CharacterBody3D
{
    private readonly Queue<IPlayerCommand> _commands = new();
    private readonly PackedScene _projectile = GD.Load<PackedScene>("res://Arena/AirborneProjectile.tscn");
    /// <summary>Gets the fixture's launch height above the actor's feet in world units.</summary>
    public const float ShotHeight = 1;
    private readonly AttackDefinition _attack = new("arena-shot", new(new(1, 20), 8, NaturalOutcomeShiftRules.Standard,
        [new(new(1, 6), 3, DamageType.Piercing)], [], AttackTags.Projectile), TimeSpan.FromSeconds(0.25), [new(new("mana"), 10)],
        new(AttackRepeatPolicy.OncePerTarget, AttackStopCondition.CombatantContact | AttackStopCondition.EnvironmentContact, TimeSpan.FromSeconds(4)));
    private GameBootstrap? _runtime;
    private Camera3D _camera = null!;
    /// <summary>Gets the identity registered in the current session.</summary>
    public CombatantId CombatantId { get; } = new(Guid.NewGuid());
    /// <summary>Gets the movement component after scene initialization.</summary>
    public MovementMotor Motor { get; private set; } = null!;
    /// <summary>Gets the yaw-only terrain aiming component.</summary>
    public MouseFacing Facing { get; private set; } = null!;
    /// <summary>Gets or sets whether hardware input drives the actor; disable to supply intent in engine tests.</summary>
    [Export]
    public bool HardwareInput { get; set; } = true;
    /// <summary>Gets or sets the intent consumed when hardware input is disabled.</summary>
    public Vector2 MovementIntent { get; set; }
    /// <summary>Gets the latest accepted attack, or null before a successful launch.</summary>
    public AttackInstance? LastAttack { get; private set; }
    /// <summary>Raised for actual launch responses, including refusals.</summary>
    public event Action<AttackStartResult>? LaunchCompleted;
    /// <summary>Raised for physical impact results and their observed input views.</summary>
    public event Action<AttackContactResult, CombatStateRead, string>? ContactCompleted;

    /// <summary>Connects the actor's focused components to the session after registration.</summary>
    /// <param name="runtime">The parent session's composition root.</param>
    /// <param name="camera">The active angled camera.</param>
    /// <param name="marker">The cursor surface marker.</param>
    public void Configure(GameBootstrap runtime, Camera3D camera, Node3D marker)
    {
        _runtime = runtime;
        _camera = camera;
        Motor = GetNode<MovementMotor>("Motor");
        Facing = GetNode<MouseFacing>("Facing");
        Facing.Configure(this, GetNode<Node3D>("Visual"), marker, camera);
    }

    /// <inheritdoc />
    public override void _PhysicsProcess(double delta)
    {
        if (_runtime is null) return;
        var state = _runtime.CombatState.Read(CombatantId, CombatantId).Source;
        while (_commands.TryDequeue(out var command)) command.Execute(this);
        var intent = HardwareInput ? Input.GetVector("arena_left", "arena_right", "arena_forward", "arena_back") : MovementIntent;
        Motor.Step(intent, _camera, delta, state.IsDefeated);
        if (HardwareInput) Facing.SampleCursor(GetViewport().GetMousePosition());
    }

    /// <inheritdoc />
    public override void _UnhandledInput(InputEvent @event)
    {
        if (!HardwareInput || @event is not InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Right } mouse) return;
        _commands.Enqueue(new ShootAtCursor(mouse.Position));
        GetViewport().SetInputAsHandled();
    }

    /// <summary>Requests one straight projectile attack toward an actual world-space aim point, spawning only after acceptance.</summary>
    /// <param name="point">World-space aim point, including the desired flight height.</param>
    /// <returns>The Application launch response.</returns>
    /// <exception cref="InvalidOperationException">The actor has not been connected to its session.</exception>
    /// <exception cref="ArgumentException">The aim point cannot produce a finite nonzero flight direction.</exception>
    public AttackStartResult RequestAttack(Vector3 point)
    {
        if (_runtime is null) throw new InvalidOperationException("Player is not configured.");
        var origin = GlobalPosition + Vector3.Up * ShotHeight;
        var direction = point - origin;
        if (!direction.IsFinite() || direction.IsZeroApprox())
            throw new ArgumentException("Aim must define a finite nonzero flight direction.", nameof(point));
        var started = _runtime.Attacks.Start(Guid.NewGuid(), CombatantId, _attack);
        LaunchCompleted?.Invoke(started);
        if (started.Attack is not { } attack) return started;
        LastAttack = attack;
        var projectile = _projectile.Instantiate<AirborneProjectile>();
        projectile.Initialize(attack, this, direction, body => ResolveBody(attack, body));
        GetParent().AddChild(projectile);
        projectile.GlobalPosition = origin;
        return started;
    }

    private void ResolveBody(AttackInstance attack, Node3D body)
    {
        var target = body is DemoCombatant combatant ? combatant.CombatantId : CombatantId;
        var input = _runtime!.CombatState.Read(CombatantId, target);
        var contact = body is DemoCombatant ? attack.ResolveContact(target) : attack.ContactEnvironment();
        ContactCompleted?.Invoke(contact, input, body.Name);
    }
}
