namespace RollForHonor.Application.Rewards;

/// <summary>Describes atomic collection of one captured world reward.</summary>
public enum GoldPickupStatus
{
    /// <summary>The wallet was credited and the reward consumed together.</summary>
    Collected,

    /// <summary>Another successful collection already consumed this reward.</summary>
    AlreadyCollected,

    /// <summary>The session changed after the wallet read; no gold was credited.</summary>
    VersionConflict,

    /// <summary>No captured reward exists for this source.</summary>
    RewardNotFound,

    /// <summary>The recipient is defeated and cannot collect world rewards.</summary>
    CollectorDefeated
}
