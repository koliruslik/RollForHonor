using System;
using RollForHonor.Application.Combat.Ports;
using RollForHonor.Domain.Combat.State;
using RollForHonor.Godot.Gameplay.Actors;

namespace RollForHonor.Godot.Gameplay.Targeting;

/// <summary>Connects shared target/combat state to one actor's session and authoritative health.</summary>
public partial class CombatAwareness : EntityComponent
{
    /// <summary>Gets controller-independent selection and combat timing.</summary>
    public EngagementState Selection { get; } = new();

    /// <summary>Gets whether this actor is currently in combat.</summary>
    public bool IsInCombat => _time is not null && Selection.IsInCombat(_time());

    private ICombatStateAdapter _state;
    private Func<TimeSpan> _time;

    /// <inheritdoc />
    public override void PhysicsStep(double delta)
    {
        if (_state is not null)
        {
            if (_state.Read(Host.CombatantId, Host.CombatantId).Source.IsDefeated)
            {
                Selection.Reset();
                return;
            }

            Selection.Advance(_time(), id => !_state.Read(Host.CombatantId, id).Target.IsDefeated);
        }
    }

    /// <summary>Connects this capability to its scene-local combat session.</summary>
    /// <param name="state">The authoritative actor reader.</param>
    /// <param name="time">The shared game clock.</param>
    public void Configure(ICombatStateAdapter state, Func<TimeSpan> time)
    {
        _state = state;
        _time = time;
    }
}
