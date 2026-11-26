using RollForHonor.Domain.Dice.Models;

namespace RollForHonor.Domain.Rewards;

/// <summary>Retains one group's actual rolls and selected outcome without generating equipment.</summary>
public sealed class LootGroupRoll
{
    /// <summary>Gets the stable rolled group identity.</summary>
    public string GroupId { get; }

    /// <summary>Gets the captured natural selection roll.</summary>
    public DiceRoll SelectionRoll { get; }

    /// <summary>Gets the already calculated nonnegative Luck bonus applied to selection only.</summary>
    public int LuckBonus { get; }

    /// <summary>Gets the selected table outcome, including an explicit empty result.</summary>
    public LootOutcome Outcome { get; }

    /// <summary>Gets the separate natural quantity d100, or null for equipment/no reward.</summary>
    public DiceRoll? QuantityRoll { get; }

    /// <summary>Gets captured generation Luck; selection may use an explicitly adjusted group and gold uses its amount policy.</summary>
    public GenerationLuck Luck { get; }

    /// <summary>Gets the capped Luck-adjusted d100, or null when the outcome is not gold.</summary>
    public decimal? EffectiveQuantityRoll => QuantityRoll is null ? null : Luck.BoostD100(QuantityRoll.Total);

    /// <summary>Gets the captured integer gold amount; zero for equipment/no reward.</summary>
    public long GoldAmount { get; }

    internal LootGroupRoll(string groupId, DiceRoll selectionRoll, int luckBonus,
        LootOutcome outcome, DiceRoll? quantityRoll, long goldAmount, GenerationLuck luck)
    {
        GroupId = groupId;
        SelectionRoll = selectionRoll;
        LuckBonus = luckBonus;
        Outcome = outcome;
        QuantityRoll = quantityRoll;
        GoldAmount = goldAmount;
        Luck = luck;
    }
}
