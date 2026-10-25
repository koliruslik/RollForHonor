namespace RollForHonor.Domain.Combat.Damage.Services;

/// <summary>
/// Reduces physical hit damage by armor / (armor + 5 * damage).
/// Larger hits receive a smaller proportional reduction from the same armor.
/// </summary>
public sealed class HitMagnitudeArmorFormula : IArmorFormula
{
    /// <summary>Calculates remaining physical damage, rounded upward.</summary>
    /// <param name="damage">Non-negative combined physical hit damage.</param>
    /// <param name="armor">Armor rating; non-positive values provide no mitigation.</param>
    /// <returns>
    /// Zero for zero input; otherwise ceil(5 * damage squared / (armor + 5 * damage)).
    /// This formula has no percentage reduction cap. The defense resolver owns
    /// the minimum physical damage guarantee after resistances.
    /// </returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="damage"/> is negative.</exception>
    public int Apply(int damage, int armor)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(damage);

        if (damage == 0 || armor <= 0)
            return damage;

        var scaledDamage = 5m * damage;
        return checked((int)Math.Ceiling(scaledDamage * damage / (armor + scaledDamage)));
    }
}
