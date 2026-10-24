using RollForHonor.Domain.Combat.Damage.Extensions;
using RollForHonor.Domain.Combat.Damage.Models;
using RollForHonor.Domain.Combat.State.Models;

namespace RollForHonor.Domain.Combat.Damage.Services;

/// <summary>
/// Applies armor to combined physical hit damage, then typed resistances and weaknesses.
/// </summary>
/// <param name="formula">The non-null, deterministic armor formula.</param>
/// <exception cref="ArgumentNullException"><paramref name="formula"/> is null.</exception>
public class DefenseResolver(IArmorFormula formula) : IDefenseResolver
{
    private readonly IArmorFormula _formula = formula ?? throw new ArgumentNullException(nameof(formula));

    /// <summary>
    /// Preserves existing calculation steps and appends armor and resistance stages
    /// without modifying the input damage or target snapshot.
    /// </summary>
    /// <param name="damage">Typed hit damage and its calculation history.</param>
    /// <param name="target">The target's armor and at most one resistance per type.</param>
    /// <returns>
    /// Typed damage after both stages. Resistance values are fractional reductions:
    /// 0.25 reduces damage by 25%, zero leaves it unchanged, and -0.25 increases it
    /// by 25%. Missing resistances count as zero. Each stage rounds each affected
    /// component upward. Empty and zero damage do not invoke the armor formula.
    /// </returns>
    /// <exception cref="ArgumentNullException">Either argument is null.</exception>
    /// <exception cref="ArgumentException">Target resistances contain duplicate types or values above one.</exception>
    /// <exception cref="InvalidOperationException">The armor formula returns damage outside zero through the physical input sum.</exception>
    /// <exception cref="OverflowException">A physical sum or scaled component exceeds the supported numeric range.</exception>
    public FinalDamage Resolve(UnmitigatedDamage damage, CombatantSnapshot target)
    {
        ArgumentNullException.ThrowIfNull(damage);
        ArgumentNullException.ThrowIfNull(target);

        if (target.Resistances.Any(r => r.Value > 1m)
            || target.Resistances.Select(r => r.Type).Distinct().Count() != target.Resistances.Count)
        {
            throw new ArgumentException(
                "Target resistances must have unique types and values no greater than one.",
                nameof(target));
        }

        var steps = damage.Breakdown.Steps.ToList();

        var afterArmor = ApplyArmor(damage.Damage, target.Armor);

        steps.Add(new DamageStep(
            DamageStepType.ArmorMitigation,
            damage.Damage,
            afterArmor));

        var afterResistances = ApplyResistances(afterArmor, target.Resistances);

        steps.Add(new DamageStep(
            DamageStepType.ResistanceMitigation,
            afterArmor,
            afterResistances));

        return new FinalDamage(new DamageBreakdown(steps), afterResistances);
    }

    private DamagePacket ApplyArmor(DamagePacket damage, int armor)
    {
        var physDamage = damage.Components
            .Where(c => c.Type.IsPhysical())
            .Sum(c => c.Amount);

        if (physDamage == 0)
            return damage;

        var finalDamage = _formula.Apply(physDamage, armor);
        if (finalDamage < 0 || finalDamage > physDamage)
        {
            throw new InvalidOperationException(
                "The armor formula must return damage between zero and the physical input sum.");
        }
        decimal damageTakenMultiplier = (decimal)finalDamage / physDamage;

        var components = damage.Components
            .Select(c => c.Type.IsPhysical()
                ? new DamageAmount(
                    c.Type,
                    checked((int)Math.Ceiling(c.Amount * damageTakenMultiplier)))
                : c)
            .ToArray();

        return new DamagePacket(components);
    }

    private static DamagePacket ApplyResistances(DamagePacket damage, IReadOnlyList<ResistanceValue> resistances)
    {
        var components = damage.Components
            .Select(component =>
            {
                var resistance = resistances
                    .SingleOrDefault(r => r.Type == component.Type)
                    ?.Value ?? 0m;

                var multiplier = 1m - resistance;
                var amount = checked((int)Math.Ceiling(component.Amount * multiplier));

                return new DamageAmount(component.Type, amount);
            }).ToArray();

        return new DamagePacket(components);
    }
}
