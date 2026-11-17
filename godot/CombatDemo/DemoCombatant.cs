using System;
using Godot;
using RollForHonor.Domain.Combat.State.Models;

namespace RollForHonor.Godot.CombatDemo;

/// <summary>Maps one test-scene body to its session combat identity without owning combat state.</summary>
public partial class DemoCombatant : StaticBody3D
{
    /// <summary>Gets the unique identity registered by the owning demo scene.</summary>
    public CombatantId CombatantId { get; } = new(Guid.NewGuid());
}
