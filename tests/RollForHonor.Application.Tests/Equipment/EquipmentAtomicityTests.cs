using RollForHonor.Application.Equipment.Models;
using RollForHonor.Application.Equipment.Ports;
using RollForHonor.Application.Equipment.Services;
using RollForHonor.Domain.Combat.State.Models;
using RollForHonor.Domain.Items.Equipment.Models;
using RollForHonor.Domain.Items.Models;
using RollForHonor.Domain.Stats;

namespace RollForHonor.Application.Tests.Equipment;

/// <summary>Verifies rejected/stale equipment proposals cannot publish partial loadouts or derived actor state.</summary>
public sealed class EquipmentAtomicityTests
{
    /// <summary>Projection failure preserves equipment, profile, resolved values, combat snapshots and resources.</summary>
    [Fact]
    public void OverflowCannotPublishAnyPartOfAnEquip()
    {
        var session = new EquipmentTestSession();
        var item = EquipmentTestSession.Item("overflow", "body-armor",
            new ItemModifier(new StatKey(StatType.Armor), StatOperation.Flat, int.MaxValue));
        session.State.RegisterOwnedItem(session.Owner, item);
        var equipment = session.State.ReadEquipment(session.Owner);
        var combat = session.State.Read(session.Owner, session.Other);
        var profile = session.State.GetStatProfile(session.Owner);
        var stats = session.State.GetStats(session.Owner);
        var resources = session.State.ReadForStart(session.Owner, "shot", Guid.NewGuid()).Resources;

        Assert.Throws<OverflowException>(() => session.Service.EquipItem(session.Owner, item.Id, EquipmentSlot.BodyArmor));

        Assert.Equal(equipment.Version, session.State.ReadEquipment(session.Owner).Version);
        Assert.Same(equipment.Loadout, session.State.ReadEquipment(session.Owner).Loadout);
        Assert.Same(equipment.NonEquipmentProfile, session.State.ReadEquipment(session.Owner).NonEquipmentProfile);
        Assert.Same(combat.Source, session.State.Read(session.Owner, session.Other).Source);
        Assert.Same(combat.Target, session.State.Read(session.Owner, session.Other).Target);
        Assert.Same(profile, session.State.GetStatProfile(session.Owner));
        Assert.Same(stats, session.State.GetStats(session.Owner));
        Assert.Equal(resources, session.State.ReadForStart(session.Owner, "shot", Guid.NewGuid()).Resources);
    }

    /// <summary>Direct port proposals cannot introduce foreign, substituted or incompatible item values.</summary>
    [Fact]
    public void AdapterRevalidatesCanonicalOwnershipAndCompatibility()
    {
        var session = new EquipmentTestSession();
        var actual = EquipmentTestSession.Item("actual", "weapon");
        var foreign = EquipmentTestSession.Item("foreign", "weapon");
        var forged = EquipmentTestSession.Item("actual", "weapon",
            new ItemModifier(new StatKey(StatType.AttackModifier), StatOperation.Flat, 100));
        session.State.RegisterOwnedItem(session.Owner, actual);
        session.State.RegisterOwnedItem(session.Other, foreign);
        var before = session.State.ReadEquipment(session.Owner);

        AssertInvalidProposal(session, before, EquipmentSlot.Weapon, foreign);
        AssertInvalidProposal(session, before, EquipmentSlot.Weapon, forged);
        AssertInvalidProposal(session, before, EquipmentSlot.Helmet, actual);

        Assert.Equal(before.Version, session.State.ReadEquipment(session.Owner).Version);
        Assert.Same(before.Loadout, session.State.ReadEquipment(session.Owner).Loadout);
        Assert.Equal(0, session.State.GetStats(session.Owner).Get(new StatKey(StatType.AttackModifier)));
    }

    /// <summary>A concurrent actor edit makes a pending equip stale without overwriting that actor edit.</summary>
    [Fact]
    public void InterveningDamageRejectsThePendingEquipmentCommit()
    {
        var session = new EquipmentTestSession();
        var item = EquipmentTestSession.Item("weapon", "weapon");
        session.State.RegisterOwnedItem(session.Owner, item);
        var current = session.State.Read(session.Owner, session.Owner).Source;
        var racing = new RacingAdapter(session.State, () => session.State.UpdateCombatant(
            new CombatantSnapshot(current.Id, 70, current.MaxHealth, current.Armor, current.Evasion, current.Resistances, current.Effects)));

        var status = new EquipmentService(racing).EquipItem(session.Owner, item.Id, EquipmentSlot.Weapon);

        Assert.Equal(EquipmentChangeStatus.VersionConflict, status);
        Assert.Equal(70, session.State.Read(session.Owner, session.Owner).Source.Health);
        Assert.Empty(session.State.ReadEquipment(session.Owner).Loadout.Slots);
    }

    /// <summary>An apparent no-op still checks version and cannot hide an intervening unequip.</summary>
    [Fact]
    public void StaleNoOpCannotMaskAnInterveningUnequip()
    {
        var session = new EquipmentTestSession();
        var item = EquipmentTestSession.Item("weapon", "weapon");
        session.State.RegisterOwnedItem(session.Owner, item);
        session.Service.EquipItem(session.Owner, item.Id, EquipmentSlot.Weapon);
        var racing = new RacingAdapter(session.State, () => session.Service.UnequipItem(session.Owner, EquipmentSlot.Weapon));

        var status = new EquipmentService(racing).EquipItem(session.Owner, item.Id, EquipmentSlot.Weapon);

        Assert.Equal(EquipmentChangeStatus.VersionConflict, status);
        Assert.Empty(session.State.ReadEquipment(session.Owner).Loadout.Slots);
    }

    /// <summary>A stale proposal cannot replace newer non-equipment stat inputs.</summary>
    [Fact]
    public void BaseStatUpdatesInvalidateEquipmentProposals()
    {
        var session = new EquipmentTestSession();
        var before = session.State.ReadEquipment(session.Owner);
        session.State.UpdateNonEquipmentStats(session.Owner, EquipmentTestSession.Profile(
            [new StatModifier(new StatKey(StatType.Armor), StatOperation.Flat, 9, "new-buff")]));
        var version = session.State.ReadEquipment(session.Owner).Version;

        var status = session.State.TryCommitEquipment(new EquipmentChangeProposal(before.Version, session.Owner, before.Loadout));

        Assert.Equal(EquipmentChangeStatus.VersionConflict, status);
        Assert.Equal(version, session.State.ReadEquipment(session.Owner).Version);
        Assert.Equal(11, session.State.GetStats(session.Owner).Get(new StatKey(StatType.Armor)));
    }

    /// <summary>Failure while updating other-source inputs preserves both currently equipped gear and its prior projection.</summary>
    [Fact]
    public void InvalidNonEquipmentInputsCannotReplaceAnEquippedProfile()
    {
        var session = new EquipmentTestSession();
        var item = EquipmentTestSession.Item("armor", "body-armor",
            new ItemModifier(new StatKey(StatType.Armor), StatOperation.Flat, 5));
        session.State.RegisterOwnedItem(session.Owner, item);
        session.Service.EquipItem(session.Owner, item.Id, EquipmentSlot.BodyArmor);
        var before = session.State.ReadEquipment(session.Owner);
        var stats = session.State.GetStats(session.Owner);
        var profile = session.State.GetStatProfile(session.Owner);

        Assert.Throws<OverflowException>(() => session.State.UpdateNonEquipmentStats(session.Owner,
            new StatProfile(before.NonEquipmentProfile.BaseValues.With(new StatKey(StatType.Armor), decimal.MaxValue))));

        Assert.Equal(before.Version, session.State.ReadEquipment(session.Owner).Version);
        Assert.Same(before.Loadout, session.State.ReadEquipment(session.Owner).Loadout);
        Assert.Same(before.NonEquipmentProfile, session.State.ReadEquipment(session.Owner).NonEquipmentProfile);
        Assert.Same(stats, session.State.GetStats(session.Owner));
        Assert.Same(profile, session.State.GetStatProfile(session.Owner));
    }

    private static void AssertInvalidProposal(EquipmentTestSession session, EquipmentStateRead before,
        EquipmentSlot slot, ItemInstance item)
    {
        var loadout = new EquipmentLoadout([new(slot, item)]);

        Assert.Throws<InvalidOperationException>(() => session.State.TryCommitEquipment(
            new EquipmentChangeProposal(before.Version, session.Owner, loadout)));
    }

    private sealed class RacingAdapter(IEquipmentStateAdapter inner, Action beforeCommit) : IEquipmentStateAdapter
    {
        /// <inheritdoc />
        public EquipmentStateRead ReadEquipment(CombatantId ownerId) => inner.ReadEquipment(ownerId);

        /// <inheritdoc />
        public EquipmentChangeStatus TryCommitEquipment(EquipmentChangeProposal proposal)
        {
            beforeCommit();

            return inner.TryCommitEquipment(proposal);
        }
    }
}
