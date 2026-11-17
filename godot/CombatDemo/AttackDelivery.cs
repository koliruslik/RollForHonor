using System;
using Godot;
using RollForHonor.Application.Combat.Models;
using RollForHonor.Domain.Combat.Attacks.Models;

namespace RollForHonor.Godot.CombatDemo;

/// <summary>Selects physical delivery of an accepted attack while leaving calculation and admission in Application.</summary>
internal interface IAttackDelivery
{
    /// <summary>Gets the source-local attack/cooldown key.</summary>
    string Key { get; }
    /// <summary>Gets the payload tags for this delivery.</summary>
    AttackTags Tags { get; }
    /// <summary>Gets immutable lifetime and contact rules for a new instance.</summary>
    AttackExecutionRules Rules { get; }
    /// <summary>Creates a physical visual after launch has been accepted.</summary>
    /// <param name="source">The source scene actor.</param>
    /// <param name="direction">Normalized world-space delivery direction.</param>
    /// <param name="attack">The accepted attack context.</param>
    /// <param name="contact">Receives physical contact results.</param>
    /// <param name="beforeImpact">Captures the contacted actor's journal inputs.</param>
    void Spawn(DemoCombatant source, Vector3 direction, AttackInstance attack,
        Action<AttackContactResult> contact, Action<DemoCombatant> beforeImpact);
}

internal sealed class ProjectileDelivery : IAttackDelivery
{
    private readonly PackedScene _scene = GD.Load<PackedScene>("res://CombatDemo/Projectile.tscn");
    /// <inheritdoc />
    public string Key => "demo-arrow";
    /// <inheritdoc />
    public AttackTags Tags => AttackTags.Projectile;
    /// <inheritdoc />
    public AttackExecutionRules Rules { get; } = new(AttackRepeatPolicy.OncePerTarget,
        AttackStopCondition.CombatantContact | AttackStopCondition.EnvironmentContact, TimeSpan.FromSeconds(3));
    /// <inheritdoc />
    public void Spawn(DemoCombatant source, Vector3 direction, AttackInstance attack,
        Action<AttackContactResult> contact, Action<DemoCombatant> beforeImpact)
    {
        var projectile = _scene.Instantiate<DemoProjectile>();
        projectile.Initialize(attack, direction * 6, contact, beforeImpact);
        source.GetParent().AddChild(projectile);
        projectile.GlobalPosition = source.GlobalPosition + direction * 0.9f;
    }
}

internal sealed class MeleeDelivery(Func<TimeSpan> now) : IAttackDelivery
{
    private readonly PackedScene _scene = GD.Load<PackedScene>("res://CombatDemo/Melee.tscn");
    /// <inheritdoc />
    public string Key => "demo-melee";
    /// <inheritdoc />
    public AttackTags Tags => AttackTags.Melee;
    /// <inheritdoc />
    public AttackExecutionRules Rules { get; } = new(AttackRepeatPolicy.OncePerTarget,
        AttackStopCondition.None, TimeSpan.FromSeconds(2));
    /// <inheritdoc />
    public void Spawn(DemoCombatant source, Vector3 direction, AttackInstance attack,
        Action<AttackContactResult> contact, Action<DemoCombatant> beforeImpact)
    {
        var hitbox = _scene.Instantiate<DemoMeleeHitbox>();
        hitbox.Initialize(attack, now, contact, beforeImpact);
        source.AddChild(hitbox);
        hitbox.GlobalPosition = source.GlobalPosition + direction * 1.2f;
    }
}
