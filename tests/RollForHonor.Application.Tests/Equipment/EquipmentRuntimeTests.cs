using RollForHonor.Application.Equipment.Models;
using RollForHonor.Domain.Items.Equipment.Models;
using RollForHonor.Domain.Items.Models;
using RollForHonor.Domain.Stats;

namespace RollForHonor.Application.Tests.Equipment;

/// <summary>Verifies owned equip/replace/unequip operations against the real shared session adapter.</summary>
public sealed class EquipmentRuntimeTests
{
    /// <summary>Repeating an equip cannot duplicate bonuses, and replacement/removal preserve other sources and owned items.</summary>
    [Fact]
    public void EquipReplaceAndUnequipRebuildOnlyEquipmentBonuses()
    {
        var armor = new StatKey(StatType.Armor);
        var external = new StatModifier(armor, StatOperation.Flat, 3, "Item:external-source");
        var session = new EquipmentTestSession([external]);
        var first = EquipmentTestSession.Item("first", "body-armor", new ItemModifier(armor, StatOperation.Flat, 5));
        var second = EquipmentTestSession.Item("second", "body-armor", new ItemModifier(armor, StatOperation.Flat, 7));
        session.State.RegisterOwnedItem(session.Owner, first);
        session.State.RegisterOwnedItem(session.Owner, second);
        var historical = session.State.ReadEquipment(session.Owner);

        Assert.Equal(EquipmentChangeStatus.Applied, session.Service.EquipItem(session.Owner, first.Id, EquipmentSlot.BodyArmor));
        Assert.Equal(10, session.State.Read(session.Owner, session.Owner).Source.Armor);
        var version = session.State.ReadEquipment(session.Owner).Version;
        Assert.Equal(EquipmentChangeStatus.Unchanged, session.Service.EquipItem(session.Owner, first.Id, EquipmentSlot.BodyArmor));
        Assert.Equal(version, session.State.ReadEquipment(session.Owner).Version);

        Assert.Equal(EquipmentChangeStatus.Applied, session.Service.EquipItem(session.Owner, second.Id, EquipmentSlot.BodyArmor));
        Assert.Equal(12, session.State.GetStats(session.Owner).Get(armor));
        Assert.Equal(EquipmentChangeStatus.Applied, session.Service.UnequipItem(session.Owner, EquipmentSlot.BodyArmor));
        Assert.Equal(5, session.State.GetStats(session.Owner).Get(armor));
        Assert.Contains(external, session.State.GetStatProfile(session.Owner).Modifiers);
        Assert.Equal(2, session.State.ReadEquipment(session.Owner).OwnedItems.Count);
        Assert.Empty(historical.Loadout.Slots);
        version = session.State.ReadEquipment(session.Owner).Version;
        Assert.Equal(EquipmentChangeStatus.Unchanged, session.Service.UnequipItem(session.Owner, EquipmentSlot.BodyArmor));
        Assert.Equal(version, session.State.ReadEquipment(session.Owner).Version);
    }

    /// <summary>Foreign/missing items and incompatible slots reject without changing any actor state.</summary>
    [Fact]
    public void OwnershipAndCompatibilityRejectionsDoNotPublish()
    {
        var session = new EquipmentTestSession();
        var foreign = EquipmentTestSession.Item("foreign", "weapon");
        var owned = EquipmentTestSession.Item("owned", "helmet");
        session.State.RegisterOwnedItem(session.Other, foreign);
        session.State.RegisterOwnedItem(session.Owner, owned);
        var before = session.State.Read(session.Owner, session.Other);

        Assert.Equal(EquipmentChangeStatus.ItemNotOwned, session.Service.EquipItem(session.Owner, foreign.Id, EquipmentSlot.Weapon));
        Assert.Equal(EquipmentChangeStatus.ItemNotOwned, session.Service.EquipItem(session.Owner, "missing", EquipmentSlot.Weapon));
        Assert.Equal(EquipmentChangeStatus.IncompatibleSlot, session.Service.EquipItem(session.Owner, owned.Id, EquipmentSlot.BodyArmor));

        Assert.Equal(before.Version, session.State.ReadEquipment(session.Owner).Version);
        Assert.Same(before.Source, session.State.Read(session.Owner, session.Other).Source);
        Assert.Empty(session.State.ReadEquipment(session.Owner).Loadout.Slots);
        Assert.Throws<ArgumentException>(() => session.State.RegisterOwnedItem(session.Owner, foreign));
    }

    /// <summary>The same owned ring cannot fill both slots, while two distinct instances contribute independently.</summary>
    [Fact]
    public void RingsRequireDistinctOwnedInstances()
    {
        var session = new EquipmentTestSession();
        var key = new StatKey(StatType.Armor);
        var first = EquipmentTestSession.Item("one", "ring", new ItemModifier(key, StatOperation.Flat, 3));
        var second = EquipmentTestSession.Item("two", "ring", new ItemModifier(key, StatOperation.Flat, 3));
        session.State.RegisterOwnedItem(session.Owner, first);
        session.State.RegisterOwnedItem(session.Owner, second);
        session.Service.EquipItem(session.Owner, first.Id, EquipmentSlot.RingLeft);
        var version = session.State.ReadEquipment(session.Owner).Version;

        Assert.Equal(EquipmentChangeStatus.AlreadyEquippedElsewhere,
            session.Service.EquipItem(session.Owner, first.Id, EquipmentSlot.RingRight));
        Assert.Equal(version, session.State.ReadEquipment(session.Owner).Version);
        Assert.Equal(EquipmentChangeStatus.Applied, session.Service.EquipItem(session.Owner, second.Id, EquipmentSlot.RingRight));

        Assert.Equal(8, session.State.GetStats(session.Owner).Get(key));
        Assert.Equal(EquipmentChangeStatus.Applied, session.Service.UnequipItem(session.Owner, EquipmentSlot.RingLeft));
        Assert.Equal(5, session.State.GetStats(session.Owner).Get(key));
        Assert.Same(second, session.State.ReadEquipment(session.Owner).Loadout.GetItem(EquipmentSlot.RingRight));
    }

    /// <summary>Non-equipment updates preserve gear, while complete-profile edits cannot overwrite active equipment contributions.</summary>
    [Fact]
    public void NonEquipmentInputsHaveAnExplicitUpdatePath()
    {
        var session = new EquipmentTestSession();
        var armor = new StatKey(StatType.Armor);
        var item = EquipmentTestSession.Item("armor", "body-armor", new ItemModifier(armor, StatOperation.Flat, 5));
        session.State.RegisterOwnedItem(session.Owner, item);
        session.Service.EquipItem(session.Owner, item.Id, EquipmentSlot.BodyArmor);
        var before = session.State.ReadEquipment(session.Owner);

        Assert.Throws<InvalidOperationException>(() => session.State.UpdateStats(session.Owner, EquipmentTestSession.Profile()));
        Assert.Equal(before.Version, session.State.ReadEquipment(session.Owner).Version);
        session.State.UpdateNonEquipmentStats(session.Owner, EquipmentTestSession.Profile(
            [new StatModifier(armor, StatOperation.Flat, 4, "buff")]));

        Assert.Equal(11, session.State.GetStats(session.Owner).Get(armor));
        Assert.Same(item, session.State.ReadEquipment(session.Owner).Loadout.GetItem(EquipmentSlot.BodyArmor));
        session.Service.UnequipItem(session.Owner, EquipmentSlot.BodyArmor);
        Assert.Equal(6, session.State.GetStats(session.Owner).Get(armor));
    }
}
