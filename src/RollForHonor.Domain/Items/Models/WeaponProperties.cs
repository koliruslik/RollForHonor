using RollForHonor.Domain.Combat.Damage.Models;

namespace RollForHonor.Domain.Items.Models;

/// <summary>Captures intrinsic weapon timing, mana cost and dice-based damage independently of affixes.</summary>
public sealed class WeaponProperties
{
    /// <summary>Gets attacks per second before character modifiers.</summary>
    public decimal AttacksPerSecond { get; }

    /// <summary>Gets the mana consumed by one attack; zero denotes no mana cost.</summary>
    public decimal ManaCost { get; }

    /// <summary>Gets immutable typed dice and flat damage components.</summary>
    public IReadOnlyList<DamageComponent> Damage { get; }

    /// <summary>Creates the intrinsic properties of one weapon base.</summary>
    /// <param name="attacksPerSecond">A positive attack frequency.</param>
    /// <param name="manaCost">A nonnegative mana cost.</param>
    /// <param name="damage">A nonempty sequence of nonnull damage components.</param>
    /// <exception cref="ArgumentOutOfRangeException">Timing or cost is invalid.</exception>
    /// <exception cref="ArgumentNullException">Damage is null.</exception>
    /// <exception cref="ArgumentException">Damage is empty or contains null.</exception>
    public WeaponProperties(decimal attacksPerSecond, decimal manaCost, IEnumerable<DamageComponent> damage)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(attacksPerSecond);
        ArgumentOutOfRangeException.ThrowIfNegative(manaCost);
        ArgumentNullException.ThrowIfNull(damage);
        var values = damage.ToArray();

        if (values.Length == 0 || values.Any(value => value is null))
        {
            throw new ArgumentException("A weapon requires nonnull damage components.", nameof(damage));
        }

        AttacksPerSecond = attacksPerSecond;
        ManaCost = manaCost;
        Damage = Array.AsReadOnly(values);
    }
}
