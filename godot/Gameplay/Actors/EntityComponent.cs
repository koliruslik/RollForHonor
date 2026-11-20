#nullable enable
using System;
using Godot;

namespace RollForHonor.Godot.Gameplay.Actors;

/// <summary>Provides one actor capability with an explicit host initialization and physics order.</summary>
public abstract partial class EntityComponent : Node
{
    /// <summary>Gets the owning actor after initialization.</summary>
    /// <exception cref="InvalidOperationException">Initialization has not completed.</exception>
    public DynamicEntity Host => _host ?? throw new InvalidOperationException("Component is not initialized.");

    /// <summary>Gets the stage in the actor-owned physics sequence.</summary>
    public virtual ActorPhysicsStage PhysicsStage => ActorPhysicsStage.Presentation;

    private DynamicEntity? _host;

    /// <summary>Connects dependencies after every direct actor component has received its host.</summary>
    public virtual void OnComponentsInitialized()
    {
    }

    /// <summary>Advances this capability once in the actor-owned physics sequence.</summary>
    /// <param name="delta">Elapsed game seconds supplied by Godot.</param>
    public virtual void PhysicsStep(double delta)
    {
    }

    internal void Initialize(DynamicEntity host)
    {
        if (_host is not null)
        {
            throw new InvalidOperationException("Component is already initialized.");
        }

        _host = host;
    }
}
