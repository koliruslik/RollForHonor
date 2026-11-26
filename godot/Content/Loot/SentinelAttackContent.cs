using System;
using RollForHonor.Application.Combat.Models;
using RollForHonor.Domain.Combat.Attacks.Models;
using RollForHonor.Domain.Combat.Damage.Models;

namespace RollForHonor.Godot.Content.Loot;

/// <summary>Shares Sentinel attack content between actual enemies and their bestiary page.</summary>
public static class SentinelAttackContent
{
    /// <summary>Creates the immutable ranged attack used by every current Sentinel.</summary>
    /// <returns>A piercing arrow with the current attack roll, cooldown and delivery policy.</returns>
    public static AttackDefinition Create() => new(
        "sentinel-shot",
        new(new(1, 20), 5, NaturalOutcomeShiftRules.Standard,
            [new(new(1, 6), 2, DamageType.Piercing)], [], AttackTags.Projectile),
        TimeSpan.FromSeconds(1.5),
        [],
        new(AttackRepeatPolicy.OncePerTarget,
            AttackStopCondition.CombatantContact | AttackStopCondition.EnvironmentContact,
            TimeSpan.FromSeconds(4)));
}
