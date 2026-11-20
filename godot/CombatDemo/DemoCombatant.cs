using System;
using Godot;
using RollForHonor.Domain.Combat.State.Models;
using RollForHonor.Godot.Gameplay.Combat;

namespace RollForHonor.Godot.CombatDemo;

/// <summary>Maps one test-scene body to its session combat identity without owning combat state.</summary>
public partial class DemoCombatant : StaticBody3D, ICombatBody
{
    /// <inheritdoc />
    public Vector3 AimPoint => GlobalPosition + Vector3.Up * 0.9f;

    /// <summary>Gets the unique identity registered by the owning demo scene.</summary>
    public CombatantId CombatantId { get; } = new(Guid.NewGuid());
}
