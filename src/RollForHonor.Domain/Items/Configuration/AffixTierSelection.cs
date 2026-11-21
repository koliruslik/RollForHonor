namespace RollForHonor.Domain.Items.Configuration;

/// <summary>Selects an explicit tier policy without embedding a Luck formula or a permanent balance choice.</summary>
public enum AffixTierSelection
{
    /// <summary>Choose the eligible tier with the greatest power requirement; equal requirements use ordinal tier IDs.</summary>
    HighestEligible,

    /// <summary>Choose uniformly among eligible tiers, including lower tiers at high power.</summary>
    UniformEligible
}
