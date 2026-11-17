#nullable enable
using System;
using Godot;
using RollForHonor.Application.Combat.Models;

namespace RollForHonor.Godot.CombatDemo;

/// <summary>Moves a demo projectile and forwards real physics contacts to an accepted attack instance.</summary>
public partial class DemoProjectile : Area3D
{
    private AttackInstance? _attack;
    private Vector3 _velocity;
    private Action<AttackContactResult>? _contact;
    private Action<DemoCombatant>? _beforeImpact;

    /// <summary>Configures the accepted attack before adding this projectile to the scene tree.</summary>
    /// <param name="attack">The already accepted attack; launching costs have already been committed.</param>
    /// <param name="velocity">World-space velocity in units per game second.</param>
    /// <param name="contact">Receives contact results, including ignored and environment contacts.</param>
    /// <param name="beforeImpact">Optional observer invoked immediately before a target calculation, for journal input capture.</param>
    /// <exception cref="ArgumentNullException">The attack or callback is null.</exception>
    /// <exception cref="InvalidOperationException">The projectile is already configured or in the tree.</exception>
    public void Initialize(AttackInstance attack, Vector3 velocity, Action<AttackContactResult> contact,
        Action<DemoCombatant>? beforeImpact = null)
    {
        ArgumentNullException.ThrowIfNull(attack);
        ArgumentNullException.ThrowIfNull(contact);
        if (_attack is not null || IsInsideTree()) throw new InvalidOperationException("Configure a fresh projectile before tree entry.");
        _attack = attack;
        _velocity = velocity;
        _contact = contact;
        _beforeImpact = beforeImpact;
    }

    /// <inheritdoc />
    public override void _Ready()
    {
        if (_attack is null) throw new InvalidOperationException("Projectile requires an accepted attack.");
        BodyEntered += OnBodyEntered;
    }

    /// <inheritdoc />
    public override void _PhysicsProcess(double delta)
    {
        if (!_attack!.UpdateLifetime()) { QueueFree(); return; }
        GlobalPosition += _velocity * (float)delta;
    }

    /// <inheritdoc />
    public override void _ExitTree()
    {
        BodyEntered -= OnBodyEntered;
        _attack?.End();
    }

    private void OnBodyEntered(Node3D body)
    {
        if (IsQueuedForDeletion() || !_attack!.IsActive) return;
        if (body is DemoCombatant actor && actor.CombatantId == _attack.SourceId) return;
        if (body is DemoCombatant combatant) _beforeImpact?.Invoke(combatant);
        var result = body is DemoCombatant target
            ? _attack.ResolveContact(target.CombatantId) : _attack.ContactEnvironment();
        _contact!(result);
        if (!_attack.IsActive) QueueFree();
    }
}
