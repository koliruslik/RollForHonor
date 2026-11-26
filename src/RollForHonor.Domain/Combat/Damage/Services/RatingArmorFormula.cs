namespace RollForHonor.Domain.Combat.Damage.Services;

/// <summary>Reduces combined physical damage using the accepted armour-rating coefficient 30.</summary>
public sealed class RatingArmorFormula : IArmorFormula
{
    /// <inheritdoc />
    public int Apply(int damage, int armor)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(damage);

        return damage == 0 || armor <= 0 ? damage
            : checked((int)decimal.Ceiling(30m * damage * damage / (armor + 30m * damage)));
    }
}
