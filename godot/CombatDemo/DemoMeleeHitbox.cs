#nullable enable
using System;
using Godot;
using RollForHonor.Application.Combat.Models;

namespace RollForHonor.Godot.CombatDemo;

/// <summary>Hosts one timed melee window and forwards actual hurtbox overlaps to Application admission.</summary>
public partial class DemoMeleeHitbox : Area3D
{
    private AttackInstance _attack = null!;
    private Func<TimeSpan> _now = null!;
    private Action<AttackContactResult> _contact = null!;
    private Action<DemoCombatant> _beforeImpact = null!;
    private TimeSpan _endsAt;
    private static readonly TimeSpan Window = TimeSpan.FromSeconds(0.45);

    /// <summary>Binds one accepted attack before this fresh window enters the tree.</summary>
    /// <param name="attack">The accepted source-bound attack.</param>
    /// <param name="now">The host's shared game-time clock.</param>
    /// <param name="contact">Receives calculated or ignored contacts.</param>
    /// <param name="beforeImpact">Captures journal inputs immediately before resolving the contacted actor.</param>
    /// <exception cref="ArgumentNullException">A dependency is null.</exception>
    /// <exception cref="InvalidOperationException">This window is already bound or in the tree.</exception>
    public void Initialize(AttackInstance attack, Func<TimeSpan> now, Action<AttackContactResult> contact,
        Action<DemoCombatant> beforeImpact)
    {
        ArgumentNullException.ThrowIfNull(attack);
        ArgumentNullException.ThrowIfNull(now);
        ArgumentNullException.ThrowIfNull(contact);
        ArgumentNullException.ThrowIfNull(beforeImpact);
        if (_attack is not null || IsInsideTree()) throw new InvalidOperationException("Bind a fresh melee window before tree entry.");
        _attack = attack;
        _now = now;
        _contact = contact;
        _beforeImpact = beforeImpact;
        _endsAt = now() + Window;
    }

    /// <inheritdoc />
    public override void _Ready()
    {
        if (_attack is null) throw new InvalidOperationException("Melee hitbox requires an accepted attack.");
        AreaEntered += OnAreaEntered;
    }

    /// <inheritdoc />
    public override void _PhysicsProcess(double delta)
    {
        if (_now() >= _endsAt) _attack.End(AttackEndReason.AnimationFinished);
        if (!_attack.UpdateLifetime()) { QueueFree(); return; }
        var progress = 1 - (_endsAt - _now()).TotalSeconds / Window.TotalSeconds;
        GetNode<MeshInstance3D>("Blade").Rotation = new(0, (float)((progress - 0.5) * Math.PI), 0);
    }

    /// <inheritdoc />
    public override void _ExitTree()
    {
        AreaEntered -= OnAreaEntered;
        _attack?.End();
    }

    private void OnAreaEntered(Area3D area)
    {
        if (IsQueuedForDeletion() || !_attack.IsActive) return;
        if (_now() >= _endsAt) { _attack.End(AttackEndReason.AnimationFinished); QueueFree(); return; }
        if (area is not DemoHurtbox hurtbox || hurtbox.Actor.CombatantId == _attack.SourceId) return;
        _beforeImpact(hurtbox.Actor);
        _contact(_attack.ResolveContact(hurtbox.Actor.CombatantId));
    }
}
