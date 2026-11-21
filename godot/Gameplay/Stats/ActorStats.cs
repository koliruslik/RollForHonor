using System;
using Godot;
using RollForHonor.Domain.Stats;
using RollForHonor.Godot.Gameplay.Actors;
using RollForHonor.Godot.Gameplay.Movement;

namespace RollForHonor.Godot.Gameplay.Stats;

/// <summary>Bridges immutable runtime attributes to Godot capabilities without owning health or calculation rules.</summary>
public partial class ActorStats : EntityComponent
{
    /// <inheritdoc />
    public override ActorPhysicsStage PhysicsStage => ActorPhysicsStage.State;

    /// <summary>Gets or sets the base definition used by the scene when registering this actor.</summary>
    [Export]
    public ActorStatDefinition Definition { get; set; } = null!;

    private MovementMotor _motor = null!;
    private Func<StatBlock> _read = null!;

    /// <inheritdoc />
    public override void OnComponentsInitialized()
    {
        _motor = Host.GetRequiredComponent<MovementMotor>();
    }

    /// <inheritdoc />
    public override void PhysicsStep(double delta)
    {
        _motor.Speed = (float)_read().Get(new(StatType.MovementSpeed));
    }

    /// <summary>Supplies the current session's immutable attribute read.</summary>
    /// <param name="read">Reads values published by the stat-owning runtime adapter.</param>
    public void Configure(Func<StatBlock> read) => _read = read;
}
