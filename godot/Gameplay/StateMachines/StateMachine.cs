#nullable enable
using System;
using System.Collections.Generic;

namespace RollForHonor.Godot.Gameplay.StateMachines;

/// <summary>Coordinates registered states with explicit transitions and rejects reentrant lifecycle transitions.</summary>
public sealed class StateMachine
{
    /// <summary>Gets the active state, or null before the initial transition.</summary>
    public IState? CurrentState { get; private set; }

    private readonly Dictionary<Type, IState> _states = new();
    private bool _transitioning;

    /// <summary>Registers one state instance per concrete type.</summary>
    /// <param name="state">The state owned by this machine.</param>
    /// <exception cref="ArgumentNullException">The state is null.</exception>
    /// <exception cref="ArgumentException">The concrete type is already registered.</exception>
    public void Register(IState state)
    {
        ArgumentNullException.ThrowIfNull(state);

        _states.Add(state.GetType(), state);
    }

    /// <summary>Exits the current state and enters a registered state; selecting the current state is a no-op.</summary>
    /// <typeparam name="T">The concrete type of the registered destination.</typeparam>
    /// <exception cref="InvalidOperationException">The destination is unregistered or a lifecycle callback requests a transition.</exception>
    /// <remarks>Lifecycle callbacks must not transition recursively. Callback exceptions propagate without rollback.</remarks>
    public void ChangeState<T>()
        where T : IState
    {
        if (_transitioning)
        {
            throw new InvalidOperationException("A state lifecycle callback cannot request another transition.");
        }

        if (!_states.TryGetValue(typeof(T), out var next))
        {
            throw new InvalidOperationException($"State {typeof(T).Name} is not registered.");
        }

        if (ReferenceEquals(CurrentState, next))
        {
            return;
        }

        _transitioning = true;

        try
        {
            CurrentState?.Exit();
            CurrentState = next;
            next.Enter();
        }
        finally
        {
            _transitioning = false;
        }
    }
}
