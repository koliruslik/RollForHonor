using RollForHonor.Domain.Combat.Damage.Models;

namespace RollForHonor.Domain.Combat.Damage.Extensions;

/// <summary>Classifies damage types for shared physical armor rules.</summary>
public static class DamageTypeExtensions
{
    /// <summary>Identifies piercing, bludgeoning, and slashing damage.</summary>
    /// <param name="type">The damage type to classify.</param>
    /// <returns>True for physical damage; false for other or undefined values.</returns>
    public static bool IsPhysical(this DamageType type) =>
        type is DamageType.Piercing
            or DamageType.Bludgeoning
            or DamageType.Slashing;
}
