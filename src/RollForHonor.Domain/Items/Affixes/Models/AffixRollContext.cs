using RollForHonor.Domain.Dice.Models;
using RollForHonor.Domain.Rewards;

namespace RollForHonor.Domain.Items.Affixes.Models;

/// <summary>Retains natural dice, normalized Luck and effective values behind a generated affix.</summary>
public sealed class AffixRollContext
{
    /// <summary>Gets the captured Luck policy; it is never reread from a live actor.</summary>
    public GenerationLuck Luck { get; }

    /// <summary>Gets the natural tier d20, or null when the caller explicitly selected a tier.</summary>
    public DiceRoll? TierRoll { get; }

    /// <summary>Gets the captured item-level progression bonus used by the live seven-tier policy.</summary>
    public int TierProgressionBonus { get; }

    /// <summary>Gets the tier total with progression and capped Luck bonuses, or null for explicit tiers.</summary>
    public int? EffectiveTierRoll => TierRoll?.Total + TierProgressionBonus + Luck.AffixTierBonus;

    /// <summary>Gets the separate natural d100 shared by all value ranges in this affix.</summary>
    public DiceRoll QualityRoll { get; }

    /// <summary>Gets min(100, natural quality + captured Luck), used for value interpolation.</summary>
    public decimal EffectiveQualityRoll => Luck.BoostD100(QualityRoll.Total);

    internal AffixRollContext(GenerationLuck luck, DiceRoll? tierRoll, DiceRoll qualityRoll, int tierProgressionBonus = 0)
    {
        TierProgressionBonus = tierProgressionBonus;
        Luck = luck;
        TierRoll = tierRoll;
        QualityRoll = qualityRoll;
    }
}
