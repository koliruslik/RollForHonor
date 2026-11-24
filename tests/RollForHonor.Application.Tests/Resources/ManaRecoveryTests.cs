using RollForHonor.Application.Resources;
using RollForHonor.Domain.Combat.State.Models;
using RollForHonor.Domain.Stats;
using RollForHonor.Infrastructure.Combat;

namespace RollForHonor.Application.Tests.Resources;

/// <summary>Verifies atomic, capped recovery with live maxima and living recipients.</summary>
public sealed class ManaRecoveryTests
{
    /// <summary>Recovery uses elapsed game time and the latest maximum, publishing only actual changes.</summary>
    [Fact]
    public void RecoveryClampsToMaximumAndDoesNotPublishFullOrDefeatedActors()
    {
        var state = new InMemoryCombatState();
        var owner = new CombatantId(Guid.NewGuid());
        state.AddStatCombatant(owner, Profile(10));
        state.UpdateStats(owner, Profile(100));
        var recovery = new ManaRecoveryService(state);
        Assert.Equal(20, recovery.Recover(owner, TimeSpan.FromSeconds(2), 10));
        Assert.Equal(70, recovery.Recover(owner, TimeSpan.FromSeconds(20), 10));
        var version = state.ReadEquipment(owner).Version;
        Assert.Equal(0, recovery.Recover(owner, TimeSpan.FromSeconds(20), 10));
        Assert.Equal(version, state.ReadEquipment(owner).Version);
        state.UpdateStats(owner, Profile(200));
        var current = state.Read(owner, owner).Source;
        state.UpdateCombatant(new(owner, 0, current.MaxHealth, current.Armor, current.Evasion, current.Resistances, current.Effects, current.Offense));
        version = state.ReadEquipment(owner).Version;
        Assert.Equal(0, recovery.Recover(owner, TimeSpan.FromSeconds(2), 10));
        Assert.Equal(version, state.ReadEquipment(owner).Version);
        Assert.Throws<ArgumentOutOfRangeException>(() => recovery.Recover(owner, TimeSpan.FromSeconds(-1), 10));
        Assert.Throws<ArgumentOutOfRangeException>(() => recovery.Recover(owner, TimeSpan.FromSeconds(1), -1));
    }

    private static StatProfile Profile(decimal mana) => new(new StatBlock([new(new(StatType.MaxMana), mana)]));
}
