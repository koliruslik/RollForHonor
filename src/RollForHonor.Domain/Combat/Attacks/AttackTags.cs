namespace RollForHonor.Domain.Combat.Attacks;

/// <summary>
/// Classifies an attack for rules and modifier selection.
/// </summary>
[Flags]
public enum AttackTags
{
    None = 0,
    Melee = 1 << 0,
    Ranged = 1 << 1,
    Projectile = 1 << 2,
    Spell = 1 << 3,
    Area = 1 << 4
}
