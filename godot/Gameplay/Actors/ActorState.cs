#nullable enable
using System;
using RollForHonor.Godot.Gameplay.StateMachines;

namespace RollForHonor.Godot.Gameplay.Actors;

/// <summary>Reflects authoritative defeat into a simple actor-mode FSM before input and movement run.</summary>
public partial class ActorState : EntityComponent
{
    /// <inheritdoc />
    public override ActorPhysicsStage PhysicsStage => ActorPhysicsStage.State;

    /// <summary>Gets whether the current actor mode is defeated.</summary>
    public bool IsDefeated => _machine.CurrentState is DefeatedState;

    /// <summary>Gets whether the actor mode permits intentional movement.</summary>
    public bool AllowsMovement => _machine.CurrentState is ActiveState;

    private readonly StateMachine _machine = new();

    private Func<bool>? _isDefeated;

    /// <inheritdoc />
    public override void OnComponentsInitialized()
    {
        _machine.Register(new ActiveState());
        _machine.Register(new DefeatedState());
        _machine.ChangeState<ActiveState>();
    }

    /// <inheritdoc />
    public override void PhysicsStep(double delta)
    {
        if (_isDefeated?.Invoke() == true)
        {
            _machine.ChangeState<DefeatedState>();
        }
        else
        {
            _machine.ChangeState<ActiveState>();
        }
    }

    /// <summary>Supplies a fresh authoritative defeat read without moving combat state into the actor.</summary>
    /// <param name="isDefeated">Reads current defeat state from the scene's combat adapter.</param>
    public void Configure(Func<bool> isDefeated) => _isDefeated = isDefeated;

    private sealed class ActiveState : IState
    {
        /// <inheritdoc />
        public void Enter()
        {
        }

        /// <inheritdoc />
        public void Exit()
        {
        }
    }

    private sealed class DefeatedState : IState
    {
        /// <inheritdoc />
        public void Enter()
        {
        }

        /// <inheritdoc />
        public void Exit()
        {
        }
    }
}
