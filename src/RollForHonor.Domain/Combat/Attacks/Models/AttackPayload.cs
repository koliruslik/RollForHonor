using RollForHonor.Domain.Combat.Damage.Models;
using RollForHonor.Domain.Combat.Effects.Models;
using RollForHonor.Domain.Dice.Models;

namespace RollForHonor.Domain.Combat.Attacks.Models;

/// <summary>
/// Describes the immutable rules and payload of an attack.
/// </summary>
public sealed record AttackPayload
{
    /// <summary>Gets the formula used to roll the attack.</summary>
    public DiceFormula AttackRoll { get; }

    /// <summary>Gets the flat modifier applied to the attack roll.</summary>
    public int AttackModifier { get; }

    /// <summary>Gets the natural-roll rules used to shift the attack outcome.</summary>
    public NaturalOutcomeShiftRules OutcomeShiftRules { get; }

    /// <summary>Gets the attack's typed damage components.</summary>
    public IReadOnlyList<DamageComponent> Damage { get; }

    /// <summary>Gets the effects carried by the attack.</summary>
    public IReadOnlyList<CombatEffectDefinition> Effects { get; }

    /// <summary>Gets the classifications used by combat rules.</summary>
    public AttackTags Tags { get; }

    /// <summary>
    /// Creates a validated attack payload.
    /// </summary>
    /// <param name="attackRoll">The formula used by the attack-roll stage.</param>
    /// <param name="attackModifier">The flat value added to the attack roll.</param>
    /// <param name="outcomeShiftRules">Natural-roll rules that shift the classified outcome.</param>
    /// <param name="damage">Typed dice and flat damage components.</param>
    /// <param name="effects">Effect definitions carried by the attack.</param>
    /// <param name="tags">Known classifications used by conditional rules.</param>
    /// <exception cref="ArgumentNullException">A reference argument is null.</exception>
    /// <exception cref="ArgumentException">A damage or effect collection contains a null item.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="tags"/> contains an unknown flag.</exception>
    public AttackPayload(
        DiceFormula attackRoll,
        int attackModifier,
        NaturalOutcomeShiftRules outcomeShiftRules,
        IReadOnlyList<DamageComponent> damage,
        IReadOnlyList<CombatEffectDefinition> effects,
        AttackTags tags)
    {
        ArgumentNullException.ThrowIfNull(attackRoll);
        ArgumentNullException.ThrowIfNull(damage);
        ArgumentNullException.ThrowIfNull(effects);
        ArgumentNullException.ThrowIfNull(outcomeShiftRules);

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
        OutcomeShiftRules = outcomeShiftRules;
        Damage = damage.ToArray();
        Effects = effects.ToArray();
        Tags = tags;
    }
}
