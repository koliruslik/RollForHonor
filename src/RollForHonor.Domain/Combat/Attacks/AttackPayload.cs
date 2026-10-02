using RollForHonor.Domain.Combat.Damage;
using RollForHonor.Domain.Combat.Effects;
using RollForHonor.Domain.Dice;

namespace RollForHonor.Domain.Combat.Attacks;

/// <summary>
/// Describes the immutable rules and payload of an attack.
/// </summary>
public sealed record AttackPayload
{
    /// <summary>Gets the formula used to roll the attack.</summary>
    public DiceFormula AttackRoll { get; }

    /// <summary>Gets the flat modifier applied to the attack roll.</summary>
    public int AttackModifier { get; }

    /// <summary>Gets the attack's typed damage components.</summary>
    public IReadOnlyList<DamageComponent> Damage { get; }

    /// <summary>Gets the effects carried by the attack.</summary>
    public IReadOnlyList<CombatEffectDefinition> Effects { get; }

    /// <summary>Gets the classifications used by combat rules.</summary>
    public AttackTags Tags { get; }

    /// <summary>
    /// Creates a validated attack payload.
    /// </summary>
    public AttackPayload(
        DiceFormula attackRoll,
        int attackModifier,
        IReadOnlyList<DamageComponent> damage,
        IReadOnlyList<CombatEffectDefinition> effects,
        AttackTags tags)
    {
        ArgumentNullException.ThrowIfNull(attackRoll);
        ArgumentNullException.ThrowIfNull(damage);
        ArgumentNullException.ThrowIfNull(effects);

        if (damage.Any(component => component is null))
        {
            throw new ArgumentException(
                "An attack cannot contain null damage components.",
                nameof(damage));
        }

        if (effects.Any(effect => effect is null))
        {
            throw new ArgumentException(
                "An attack cannot contain null effects.",
                nameof(effects));
        }

        const AttackTags knownTags =
            AttackTags.Melee |
            AttackTags.Ranged |
            AttackTags.Projectile |
            AttackTags.Spell |
            AttackTags.Area;

        if ((tags & ~knownTags) != 0)
        {
            throw new ArgumentOutOfRangeException(nameof(tags));
        }

        AttackRoll = attackRoll;
        AttackModifier = attackModifier;
        Damage = damage.ToArray();
        Effects = effects.ToArray();
        Tags = tags;
    }
}
