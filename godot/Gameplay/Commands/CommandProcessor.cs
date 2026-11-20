using System;
using System.Collections.Generic;
using RollForHonor.Godot.Gameplay.Actors;

namespace RollForHonor.Godot.Gameplay.Commands;

/// <summary>Consumes FIFO commands once, without automatic retries, buffering windows or hidden cooldown waits.</summary>
public partial class CommandProcessor : EntityComponent
{
    /// <inheritdoc />
    public override ActorPhysicsStage PhysicsStage => ActorPhysicsStage.Commands;

    /// <summary>Gets the reserved maximum queue age; null currently indicates that age-based expiry is not implemented.</summary>
    /// <remarks>TODO: define command retention, its clock and retry policy before implementing expiry. This placeholder is not configurable; commands are currently consumed once at the next command stage.</remarks>
    public TimeSpan? CommandLifetime { get; } = null;

    private readonly Queue<IEntityCommand> _commands = new();

    /// <inheritdoc />
    public override void PhysicsStep(double delta)
    {
        while (_commands.TryDequeue(out var command))
        {
            command.Execute(Host);
        }
    }

    /// <inheritdoc />
    public override void _ExitTree() => _commands.Clear();

    /// <summary>Adds an intention for the next actor command stage.</summary>
    /// <param name="command">The intention consumed once in FIFO order.</param>
    /// <exception cref="ArgumentNullException">The command is null.</exception>
    public void Enqueue(IEntityCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);
        _commands.Enqueue(command);
    }
}
