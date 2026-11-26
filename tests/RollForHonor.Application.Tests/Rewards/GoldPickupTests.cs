using RollForHonor.Application.Rewards;
using RollForHonor.Domain.Combat.State.Models;
using RollForHonor.Domain.Stats;
using RollForHonor.Domain.Rewards;
using RollForHonor.Infrastructure.Dice;
using RollForHonor.Infrastructure.Combat;

namespace RollForHonor.Application.Tests.Rewards;

/// <summary>Verifies captured defeat rewards, wallet isolation and atomic single-use collection.</summary>
public sealed class GoldPickupTests
{
    /// <summary>Capturing a defeat does not credit gold; repeated capture preserves the first amount and version.</summary>
    [Fact]
    public void CaptureRetainsFirstAmountWithoutCreditingWallet()
    {
        var (state, owner, source) = Session();
        var captured = state.CaptureGoldReward(source, 75);
        var version = state.ReadGoldWallet(owner).Version;

        Assert.Same(captured, state.CaptureGoldReward(source, 500));
        Assert.Same(captured, state.ReadGoldReward(source));
        Assert.Equal(0, state.ReadGoldWallet(owner).Balance);
        Assert.Equal(version, state.ReadGoldWallet(owner).Version);
    }

    /// <summary>Successful collection consumes the captured amount once without changing combat resources or inventory.</summary>
    [Fact]
    public void PickupCreditsOnceAndPreservesCombatState()
    {
        var (state, owner, source) = Session();
        var reward = state.CaptureGoldReward(source, 75);
        var equipment = state.ReadEquipment(owner);
        var actor = state.Read(owner, owner).Source;
        var resources = state.ReadForStart(owner, "attack", Guid.NewGuid()).Resources;
        var service = new GoldPickupService(state);

        Assert.Equal(GoldPickupStatus.Collected, service.Collect(owner, source));
        var wallet = state.ReadGoldWallet(owner);
        Assert.Equal(75, wallet.Balance);
        Assert.Null(state.ReadGoldReward(source));
        Assert.Equal(GoldPickupStatus.AlreadyCollected, service.Collect(owner, source));
        Assert.Equal(wallet, state.ReadGoldWallet(owner));
        Assert.Same(reward, state.CaptureGoldReward(source, 100));
        Assert.Null(state.ReadGoldReward(source));
        Assert.Same(equipment.Loadout, state.ReadEquipment(owner).Loadout);
        Assert.Same(actor, state.Read(owner, owner).Source);
        Assert.Equal(resources, state.ReadForStart(owner, "attack", Guid.NewGuid()).Resources);
    }

    /// <summary>A duplicate reward cannot credit a second collector's wallet.</summary>
    [Fact]
    public void RewardCannotBeCollectedByTwoOwners()
    {
        var (state, owner, source) = Session();
        var other = new CombatantId(Guid.NewGuid());
        state.AddStatCombatant(other, Profile());
        state.CaptureGoldReward(source, 10);
        var service = new GoldPickupService(state);

        Assert.Equal(GoldPickupStatus.Collected, service.Collect(owner, source));
        Assert.Equal(GoldPickupStatus.AlreadyCollected, service.Collect(other, source));
        Assert.Equal(0, state.ReadGoldWallet(other).Balance);
    }

    /// <summary>Stale collection preserves both wallet balance and the original captured reward.</summary>
    [Fact]
    public void ConflictDoesNotConsumeOrRerollReward()
    {
        var (state, owner, source) = Session();
        var reward = state.CaptureGoldReward(source, 75);
        var read = state.ReadGoldWallet(owner);
        state.UpdateNonEquipmentStats(owner, Profile());
        var version = state.ReadGoldWallet(owner).Version;

        Assert.Equal(GoldPickupStatus.VersionConflict, state.TryCollectGold(owner, source, read.Version));
        Assert.Equal(version, state.ReadGoldWallet(owner).Version);
        Assert.Equal(0, state.ReadGoldWallet(owner).Balance);
        Assert.Same(reward, state.ReadGoldReward(source));
        Assert.Equal(GoldPickupStatus.Collected, new GoldPickupService(state).Collect(owner, source));
        Assert.Equal(75, state.ReadGoldWallet(owner).Balance);
    }

    /// <summary>Defeated collectors and unknown rewards leave wallet and reward state unchanged.</summary>
    [Fact]
    public void RejectedRequestsDoNotAdvanceVersion()
    {
        var (state, owner, source) = Session();
        var service = new GoldPickupService(state);
        var version = state.ReadGoldWallet(owner).Version;
        Assert.Equal(GoldPickupStatus.RewardNotFound, service.Collect(owner, source));
        Assert.Equal(version, state.ReadGoldWallet(owner).Version);
        state.CaptureGoldReward(source, 10);
        var actor = state.Read(owner, owner).Source;
        state.UpdateCombatant(new(owner, 0, actor.MaxHealth, actor.Armor, actor.Evasion, actor.Resistances, actor.Effects, actor.Offense));
        var before = state.ReadGoldWallet(owner);

        Assert.Equal(GoldPickupStatus.CollectorDefeated, service.Collect(owner, source));
        Assert.Equal(before, state.ReadGoldWallet(owner));
        Assert.NotNull(state.ReadGoldReward(source));
    }

    /// <summary>Reward capture requires an authoritative defeated source, not a caller's unverified notification.</summary>
    [Fact]
    public void LivingActorCannotProduceGold()
    {
        var (state, owner, _) = Session();
        var before = state.ReadGoldWallet(owner);

        Assert.Throws<InvalidOperationException>(() => state.CaptureGoldReward(owner, 10));
        Assert.Equal(before, state.ReadGoldWallet(owner));
        Assert.Null(state.ReadGoldReward(owner));
        Assert.Throws<ArgumentOutOfRangeException>(() => state.CaptureGoldReward(owner, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => state.CaptureGoldReward(owner, -1));
    }

    /// <summary>Balance overflow publishes neither wallet credit nor reward consumption.</summary>
    [Fact]
    public void OverflowRetainsTheUncollectedReward()
    {
        var (state, owner, source) = Session();
        state.CaptureGoldReward(source, long.MaxValue);
        var service = new GoldPickupService(state);
        Assert.Equal(GoldPickupStatus.Collected, service.Collect(owner, source));
        var next = new CombatantId(Guid.NewGuid());
        state.AddCombatant(new(next, 0, 1, 0, 0, [], []));
        var reward = state.CaptureGoldReward(next, 1);
        var before = state.ReadGoldWallet(owner);

        Assert.Throws<OverflowException>(() => service.Collect(owner, next));
        Assert.Equal(before, state.ReadGoldWallet(owner));
        Assert.Same(reward, state.ReadGoldReward(next));
    }

    /// <summary>Fresh sessions do not inherit another session's gold or captured rewards.</summary>
    [Fact]
    public void WalletsRemainSessionLocal()
    {
        var (state, owner, source) = Session();
        state.CaptureGoldReward(source, 100);
        new GoldPickupService(state).Collect(owner, source);
        var fresh = new InMemoryCombatState();
        fresh.AddStatCombatant(owner, Profile());

        Assert.Equal(0, fresh.ReadGoldWallet(owner).Balance);
        Assert.Null(fresh.ReadGoldReward(source));
    }

    /// <summary>Pickup and repeated capture preserve the exact generation dice, Luck and amount.</summary>
    [Fact]
    public void GoldGenerationContextSurvivesCollectionAndCaptureRetries()
    {
        var (state, owner, source) = Session();
        var group = new LootGroupDefinition("gold", new(1, 20), [new(1, LootOutcome.GoldEntry(new GoldRange(6, 14)))]);
        var roll = Assert.Single(new LootGroupRoller().Roll([group], 0, new RandomDiceRoller(new Random(724)), luck: 10));
        var captured = state.CaptureGoldReward(source, roll.GoldAmount, roll);

        Assert.Same(roll, captured.RollContext);
        Assert.NotNull(captured.RollContext);
        Assert.Equal(10, captured.RollContext.Luck.Value);
        Assert.Equal(100, captured.RollContext.QuantityRoll!.Formula.Sides);
        Assert.Equal(GoldPickupStatus.Collected, new GoldPickupService(state).Collect(owner, source));
        Assert.Same(captured, state.CaptureGoldReward(source, 999));
        Assert.Same(roll, captured.RollContext);
        Assert.Equal(roll.GoldAmount, state.ReadGoldWallet(owner).Balance);
        Assert.Throws<ArgumentException>(() => new GoldReward(source, roll.GoldAmount + 1, roll));
    }

    private static (InMemoryCombatState State, CombatantId Owner, CombatantId Source) Session()
    {
        var state = new InMemoryCombatState();
        var owner = new CombatantId(Guid.NewGuid());
        var source = new CombatantId(Guid.NewGuid());
        state.AddStatCombatant(owner, Profile());
        state.AddCombatant(new(source, 0, 1, 0, 0, [], []));

        return (state, owner, source);
    }

    private static StatProfile Profile() => new(new StatBlock(
    [
        new(new(StatType.MaxHealth), 100),
        new(new(StatType.MaxMana), 100)
    ]));
}
