#nullable enable
using System;
using Godot;
using RollForHonor.Application.Combat.Models;
using RollForHonor.Application.Combat.Ports;
using RollForHonor.Application.Combat.Services;
using RollForHonor.Domain.Combat.Attacks.Models;
using RollForHonor.Godot.Gameplay.Actors;
using RollForHonor.Godot.Gameplay.Projectiles;

namespace RollForHonor.Godot.Gameplay.Combat;

/// <summary>Starts accepted attacks and maps physical contacts to the current Application combat session.</summary>
/// <remarks>Temporary single-projectile implementation. When multiple attack types are introduced, extract a convenient attack selection and delivery model rather than expanding this component with attack-specific branches. The final format is not decided.</remarks>
public partial class ProjectileAttack : EntityComponent
{
    /// <summary>Gets the projectile launch height above the actor's feet in world units.</summary>
    public const float ShotHeight = 1;

    /// <summary>Gets the latest accepted attack, or null before a successful launch.</summary>
    public AttackInstance? LastAttack { get; private set; }

    /// <summary>Raised for actual launch responses, including refusals.</summary>
    public event Action<AttackStartResult>? LaunchCompleted;

    /// <summary>Raised for physical contact results and their observed input views.</summary>
    public event Action<AttackContactResult, CombatStateRead, string>? ContactCompleted;

    private AttackInitiationService? _attacks;

    private ICombatStateAdapter _state = null!;
    private AttackDefinition _attack = null!;

    private PackedScene _projectile = null!;

    /// <summary>Supplies scene-owned combat services, attack content and delivery scene.</summary>
    /// <param name="attacks">The current session's launch service.</param>
    /// <param name="state">The adapter supplying consistent combat views for contact presentation.</param>
    /// <param name="attack">The attack definition selected by the scene.</param>
    /// <param name="projectile">The physical projectile scene instantiated after launch acceptance.</param>
    public void Configure(AttackInitiationService attacks, ICombatStateAdapter state, AttackDefinition attack, PackedScene projectile)
    {
        _attacks = attacks;
        _state = state;
        _attack = attack;
        _projectile = projectile;
    }

    /// <summary>Requests one straight projectile attack toward an actual world-space aim point, spawning only after acceptance.</summary>
    /// <param name="point">World-space aim point, including the desired flight height.</param>
    /// <returns>The Application launch response.</returns>
    /// <exception cref="InvalidOperationException">The actor has not been connected to its session.</exception>
    /// <exception cref="ArgumentException">The aim point cannot produce a finite nonzero flight direction.</exception>
    public AttackStartResult RequestAttack(Vector3 point)
    {
        if (_attacks is null)
        {
            throw new InvalidOperationException("Attack component is not configured.");
        }

        var origin = Host.GlobalPosition + Vector3.Up * ShotHeight;
        var direction = point - origin;
        if (!direction.IsFinite() || direction.IsZeroApprox())
        {
            throw new ArgumentException("Aim must define a finite nonzero flight direction.", nameof(point));
        }

        var started = _attacks.Start(Guid.NewGuid(), Host.CombatantId, _attack);
        LaunchCompleted?.Invoke(started);
        if (started.Attack is not { } attack)
        {
            return started;
        }

        LastAttack = attack;
        var projectile = _projectile.Instantiate<AirborneProjectile>();
        projectile.Initialize(attack, Host, direction, body => ResolveBody(attack, body));
        Host.GetParent().AddChild(projectile);
        projectile.GlobalPosition = origin;

        return started;
    }

    private void ResolveBody(AttackInstance attack, Node3D body)
    {
        var target = body is ICombatBody combatant ? combatant.CombatantId : Host.CombatantId;
        var input = _state.Read(Host.CombatantId, target);
        var contact = body is ICombatBody ? attack.ResolveContact(target) : attack.ContactEnvironment();
        ContactCompleted?.Invoke(contact, input, body.Name);
    }
}
