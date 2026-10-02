using RollForHonor.Domain.Dice;

namespace RollForHonor.Domain.Combat.Damage;

public sealed record DamageComponent
{
    public DiceFormula Dice { get; }

    public int FlatDamage { get; }

    public DamageType Type { get; }

    public DamageComponent(
        DiceFormula dice,
        int flatDamage,
        DamageType type)
    {
        ArgumentNullException.ThrowIfNull(dice);

        if (!Enum.IsDefined(type))
        {
            throw new ArgumentOutOfRangeException(nameof(type));
        }

        Dice = dice;
        FlatDamage = flatDamage;
        Type = type;
    }
}
