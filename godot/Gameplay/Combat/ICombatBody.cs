using Godot;
using RollForHonor.Domain.Combat.State.Models;

namespace RollForHonor.Godot.Gameplay.Combat;

/// <summary>Maps a physical body to combat identity and a world-space aiming point.</summary>
public interface ICombatBody
{
    /// <summary>Gets the identity registered in the current combat session.</summary>
    CombatantId CombatantId { get; }

    /// <summary>Gets the world-space point used when aiming at this body's center.</summary>
    Vector3 AimPoint { get; }
}
