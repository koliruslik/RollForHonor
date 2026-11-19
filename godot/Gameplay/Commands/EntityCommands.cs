using Godot;
using RollForHonor.Godot.Gameplay.Actors;
using RollForHonor.Godot.Gameplay.Combat;

namespace RollForHonor.Godot.Gameplay.Commands;

/// <summary>Represents a discrete actor intention consumed once during physics.</summary>
public interface IEntityCommand
{
    /// <summary>Attempts the intention through the recipient's capabilities.</summary>
    /// <param name="actor">The actor receiving the command.</param>
    void Execute(DynamicEntity actor);
}

/// <summary>Requests one shot toward a world-space point, independent of its input source.</summary>
/// <param name="Point">The world-space aiming point sampled by the controller.</param>
public sealed record AttackAt(Vector3 Point) : IEntityCommand
{
    /// <inheritdoc />
    public void Execute(DynamicEntity actor) => actor.GetRequiredComponent<ProjectileAttack>().RequestAttack(Point);
}
