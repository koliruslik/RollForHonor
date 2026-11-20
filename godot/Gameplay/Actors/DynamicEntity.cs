#nullable enable
using System;
using System.Linq;
using Godot;
using RollForHonor.Domain.Combat.State.Models;
using RollForHonor.Godot.Gameplay.Combat;

namespace RollForHonor.Godot.Gameplay.Actors;

/// <summary>Owns a 3D actor's identity, component lifecycle and ordered physics updates.</summary>
public abstract partial class DynamicEntity : CharacterBody3D, ICombatBody
{
    /// <inheritdoc />
    public CombatantId CombatantId { get; } = new(Guid.NewGuid());

    /// <inheritdoc />
    public Vector3 AimPoint => GlobalPosition + Vector3.Up * 0.9f;

    /// <summary>Gets whether scene-owned dependencies have been supplied.</summary>
    protected bool IsConfigured { get; set; }

    private EntityComponent[] _components = [];

    /// <inheritdoc />
    public override void _Ready()
    {
        _components = GetChildren()
            .OfType<EntityComponent>()
            .OrderBy(component => component.PhysicsStage)
            .ToArray();

        foreach (var component in _components)
        {
            component.Initialize(this);
        }

        foreach (var component in _components)
        {
            component.OnComponentsInitialized();
        }
    }

    /// <inheritdoc />
    public override void _PhysicsProcess(double delta)
    {
        if (!IsConfigured)
        {
            return;
        }

        foreach (var component in _components)
        {
            component.PhysicsStep(delta);
        }
    }

    /// <summary>Finds one required capability among this actor's direct components.</summary>
    /// <typeparam name="T">The component type required by the caller.</typeparam>
    /// <returns>The single matching component.</returns>
    /// <exception cref="InvalidOperationException">The component is missing or ambiguous.</exception>
    public T GetRequiredComponent<T>()
        where T : EntityComponent => _components
            .OfType<T>()
            .Single();
}
