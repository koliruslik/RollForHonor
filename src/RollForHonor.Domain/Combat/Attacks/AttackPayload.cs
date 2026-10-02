using RollForHonor.Domain.Combat.Damage;
using RollForHonor.Domain.Combat.Effects;
using RollForHonor.Domain.Dice;

namespace RollForHonor.Domain.Combat.Attacks;

public sealed record AttackPayload
{
    public DiceFormula AttackRoll { get; }

    public int AttackModifier { get; }

    public IReadOnlyList<DamageComponent> Damage { get; }

    public IReadOnlyList<CombatEffectDefinition> Effects { get; }

    public AttackTags Tags { get; }

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
