using RollForHonor.Domain.Combat.Attacks.Models;
using RollForHonor.Domain.Combat.Attacks.Services;
using RollForHonor.Domain.Combat.Damage.Configuration;
using RollForHonor.Domain.Combat.Damage.Models;
using RollForHonor.Domain.Combat.State.Models;
using RollForHonor.Domain.Dice.Models;

namespace RollForHonor.Domain.Combat.Damage.Services;

/// <summary>Estimates normalized full-health EHP by enumerating the actual rating rules without rolling dice.</summary>
public sealed class EffectiveHealthCalculator
{
    private readonly RatingAttackOutcomePolicy _quality = new();
    private readonly AttackQualityDamageResolver _scaling = new(new DamageMultipliers(0, 0.5m, 1, 2));
    private readonly DefenseResolver _defenses = new(new RatingArmorFormula());

    /// <summary>Compares expected loss against the same attack hitting a target without armor, evasion or resistances.</summary>
    /// <param name="target">Current projected defenses; maximum HP is used, not wounded current HP.</param>
    /// <param name="type">The single incoming hit component, defaulting to physical piercing.</param>
    /// <param name="hitDamage">Positive pre-quality damage; armor is evaluated after quality rounding for every face.</param>
    /// <param name="accuracy">The fixed incoming accuracy bonus, excluding any critical check.</param>
    /// <returns>Maximum HP times baseline loss divided by defended loss; null means zero expected loss (immunity).</returns>
    /// <exception cref="ArgumentNullException">Target is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">The damage type is undefined or hit damage is not positive.</exception>
    /// <exception cref="OverflowException">Accuracy totals or defended damage exceed supported ranges.</exception>
    public decimal? Calculate(CombatantSnapshot target, DamageType type = DamageType.Piercing, int hitDamage = 10, int accuracy = 4)
    {
        ArgumentNullException.ThrowIfNull(target);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(hitDamage);

        if (!Enum.IsDefined(type))
        {
            throw new ArgumentOutOfRangeException(nameof(type));
        }

        return Calculate(target, new DamagePacket([new(type, hitDamage)]), accuracy);
    }

    /// <summary>Evaluates all components of a mixed hit together through the actual quality and defense rules.</summary>
    /// <param name="target">Current projected defenses; maximum HP is used.</param>
    /// <param name="packet">The typed reference hit, with a strictly positive total amount.</param>
    /// <param name="accuracy">Incoming accuracy bonus, excluding critical checks.</param>
    /// <returns>Normalized full-health EHP; null means the entire packet causes zero expected loss.</returns>
    /// <exception cref="ArgumentNullException">Target or packet is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">The packet has no positive damage.</exception>
    /// <exception cref="OverflowException">Damage sums or accuracy totals exceed supported ranges.</exception>
    public decimal? Calculate(CombatantSnapshot target, DamagePacket packet, int accuracy = 4)
    {
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(packet);
        var hitDamage = packet.Sum();
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(hitDamage);

        var baseline = new CombatantSnapshot(target.Id, target.MaxHealth, target.MaxHealth, 0, 0, [], []);
        var attack = new AttackPayload(new(1, 20), accuracy, NaturalOutcomeShiftRules.Standard, [], [], AttackTags.None);
        var raw = new DamageCalculationResult(new DamageBreakdown([new(DamageStepType.FlatBonus, new DamagePacket([]), packet, flatBonus: hitDamage)]), packet);
        decimal baselineLoss = 0;
        decimal defendedLoss = 0;

        for (var face = 1; face <= 20; face++)
        {
            var roll = new AttackRollResult(new DiceRoll(attack.AttackRoll, [face], face), accuracy, checked(face + accuracy));
            var bareQuality = _quality.Determine(attack, baseline, baseline, roll);
            var targetQuality = _quality.Determine(attack, baseline, target, roll);
            baselineLoss += _defenses.Resolve(_scaling.Resolve(raw, bareQuality), baseline).Sum();
            defendedLoss += _defenses.Resolve(_scaling.Resolve(raw, targetQuality), target).Sum();
        }

        return defendedLoss == 0 ? null : target.MaxHealth * baselineLoss / defendedLoss;
    }
}
