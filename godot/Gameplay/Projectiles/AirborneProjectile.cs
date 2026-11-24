#nullable enable
using System;
using Godot;
using RollForHonor.Application.Combat.Models;

namespace RollForHonor.Godot.Gameplay.Projectiles;

/// <summary>Delivers an accepted attack along a fixed three-dimensional direction until physical contact or expiry.</summary>
public partial class AirborneProjectile : CharacterBody3D
{
    /// <summary>Gets or sets travel speed in world units per game second.</summary>
    [Export]
    public float Speed { get; set; } = 16;

    /// <summary>Gets the launch direction, which remains unchanged during flight.</summary>
    public Vector3 Direction { get; private set; }

    private AttackInstance _attack = null!;

    private PhysicsBody3D _source = null!;
    private Action<Node3D> _contact = null!;

    /// <inheritdoc />
    public override void _Ready()
    {
        if (_attack is null || !float.IsFinite(Speed) || Speed <= 0)
        {
            throw new InvalidOperationException("Configure an accepted attack and a positive finite speed.");
        }

        AddCollisionExceptionWith(_source);
    }

    /// <inheritdoc />
    public override void _PhysicsProcess(double delta)
    {
        if (!_attack.UpdateLifetime())
        {
            QueueFree();
            return;
        }

        var collision = MoveAndCollide(Direction * Speed * (float)delta);
        if (collision is null)
        {
            return;
        }

        if (collision.GetCollider() is Node3D body)
        {
            _contact(body);
        }

        QueueFree();
    }

    /// <inheritdoc />
    public override void _ExitTree() => _attack?.End();

    /// <summary>Ends the attack and stops physical delivery when its arena round is over.</summary>
    public void Cancel()
    {
        _attack?.End();
        SetPhysicsProcess(false);
        QueueFree();
    }

    /// <summary>Configures a fresh projectile before scene-tree entry.</summary>
    /// <param name="attack">The accepted attack context.</param>
    /// <param name="source">The source body excluded from projectile collision.</param>
    /// <param name="direction">A finite, nonzero three-dimensional direction, normalized during initialization.</param>
    /// <param name="contact">Receives the first physical actor, obstacle or terrain contact.</param>
    /// <exception cref="ArgumentNullException">A required dependency is null.</exception>
    /// <exception cref="ArgumentException">The direction is zero or nonfinite.</exception>
    /// <exception cref="InvalidOperationException">The projectile is already configured or in the tree.</exception>
    public void Initialize(AttackInstance attack, PhysicsBody3D source, Vector3 direction, Action<Node3D> contact)
    {
        ArgumentNullException.ThrowIfNull(attack);
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(contact);
        if (_attack is not null || IsInsideTree())
        {
            throw new InvalidOperationException("Configure a fresh projectile before tree entry.");
        }

        if (!direction.IsFinite() || direction.IsZeroApprox())
        {
            throw new ArgumentException("A finite nonzero flight direction is required.", nameof(direction));
        }

        _attack = attack;
        _source = source;
        Direction = direction.Normalized();
        _contact = contact;
    }
}
