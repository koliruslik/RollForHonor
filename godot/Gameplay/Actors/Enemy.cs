using RollForHonor.Godot.Gameplay.AI;

namespace RollForHonor.Godot.Gameplay.Actors;

/// <summary>Hosts shared actor capabilities driven by an enemy controller.</summary>
public partial class Enemy : DynamicEntity
{
    /// <summary>Gets the controller that supplies movement and attack intentions.</summary>
    public EnemyController Controls => GetRequiredComponent<EnemyController>();
}
