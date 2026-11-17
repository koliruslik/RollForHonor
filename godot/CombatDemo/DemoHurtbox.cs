using Godot;

namespace RollForHonor.Godot.CombatDemo;

/// <summary>Maps a melee contact area to its owning actor without calculating damage or tracking attack admission.</summary>
public partial class DemoHurtbox : Area3D
{
    /// <summary>Gets the parent actor whose combat identity this hurtbox represents.</summary>
    public DemoCombatant Actor => GetParent<DemoCombatant>();
}
