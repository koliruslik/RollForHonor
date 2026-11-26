namespace RollForHonor.Domain.Stats;

/// <summary>Identifies a calculated character attribute.</summary>
public enum StatType
{
    /// <summary>Maximum health, separate from current HP.</summary>
    MaxHealth,

    /// <summary>Maximum mana, separate from current balance.</summary>
    MaxMana,

    /// <summary>Armor used by the existing defense formula.</summary>
    Armor,

    /// <summary>Evasion used by d20 hit quality.</summary>
    Evasion,

    /// <summary>Maximum intentional movement speed in world units per second.</summary>
    MovementSpeed,

    /// <summary>Additional modifier to the attack payload's d20 total.</summary>
    AttackModifier,

    /// <summary>Reserved independent critical chance; not consumed by current d20 rules.</summary>
    CriticalChance,

    /// <summary>Reserved independent critical multiplier; current quality coefficients remain authoritative.</summary>
    CriticalMultiplier,

    /// <summary>Typed fractional damage reduction; negative values are weaknesses.</summary>
    Resistance,

    /// <summary>Outgoing damage coefficient; global and typed increases share one additive pool.</summary>
    DamageMultiplier,

    /// <summary>Typed flat damage added to attacks before source scaling, hit quality and defenses; not added to periodic ticks.</summary>
    AddedDamage,

    /// <summary>Allocated Strength points; derived damage rules are not configured.</summary>
    Strength,

    /// <summary>Allocated Dexterity points; derived speed rules are not configured.</summary>
    Dexterity,

    /// <summary>Allocated Constitution points; derived resource rules are not configured.</summary>
    Constitution,

    /// <summary>Allocated Intelligence points; derived magic rules are not configured.</summary>
    Intelligence,

    /// <summary>Allocated Luck points used by captured affix-tier, quality and gold-amount rolls.</summary>
    Luck,

    /// <summary>Flat health points recovered per game second.</summary>
    HealthRecovery,

    /// <summary>Flat mana points recovered per game second.</summary>
    ManaRecovery,

    /// <summary>Multiplicative attack cadence coefficient, independent of movement speed.</summary>
    AttackSpeed,

    /// <summary>Number of extra typed d4 rolled on an attack impact.</summary>
    AddedDamageD4,

    /// <summary>Number of extra typed d6 rolled on an attack impact.</summary>
    AddedDamageD6,

    /// <summary>Number of extra typed d8 rolled on an attack impact.</summary>
    AddedDamageD8,

    /// <summary>Number of extra typed d10 rolled on an attack impact.</summary>
    AddedDamageD10,

    /// <summary>Number of extra typed d12 rolled on an attack impact.</summary>
    AddedDamageD12,

    /// <summary>Bonus on the separate critical d20; independent of attack accuracy.</summary>
    CriticalRollBonus,

    /// <summary>Character equipment level, capped at ten without automatic stat growth.</summary>
    CharacterLevel
}
