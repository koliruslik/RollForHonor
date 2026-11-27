namespace RollForHonor.Domain.Combat.Damage.Models;

/// <summary>
/// Identifies resistible hit categories and internal pure periodic damage.
/// </summary>
public enum DamageType
{
    /// <summary>Physical puncture damage.</summary>
    Piercing,

    /// <summary>Physical impact damage.</summary>
    Bludgeoning,

    /// <summary>Physical cutting damage.</summary>
    Slashing,

    /// <summary>Resistible fire damage.</summary>
    Fire,

    /// <summary>Resistible cold damage.</summary>
    Cold,

    /// <summary>Resistible lightning damage.</summary>
    Lightning,

    /// <summary>Unmitigated periodic damage; unavailable as a hit or equipment damage category.</summary>
    Pure
}
