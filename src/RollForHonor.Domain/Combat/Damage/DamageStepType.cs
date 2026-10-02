namespace RollForHonor.Domain.Combat.Damage;

/// <summary>
/// Identifies a stage recorded in a damage calculation.
/// </summary>
public enum DamageStepType
{
    WeaponRoll,
    FlatBonus,
    AttackQuality,
    AttackerModifier,
    DamageConversion,
    AuraAbsorption,
    ShieldAbsorption,
    ArmorMitigation,
    ResistanceMitigation,
    Finalization
}
