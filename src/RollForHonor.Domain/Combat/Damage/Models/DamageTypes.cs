namespace RollForHonor.Domain.Combat.Damage.Models;

/// <summary>Separates resistible hit categories from internal unmitigated periodic damage.</summary>
public static class DamageTypes
{
    /// <summary>Gets the six hit categories used by equipment, resistances, reference and EHP.</summary>
    public static IReadOnlyList<DamageType> HitTypes { get; } = Array.AsReadOnly(new[]
    {
        DamageType.Piercing, DamageType.Bludgeoning, DamageType.Slashing,
        DamageType.Fire, DamageType.Cold, DamageType.Lightning
    });
}
