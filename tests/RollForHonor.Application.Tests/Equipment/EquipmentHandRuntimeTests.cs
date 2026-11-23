using RollForHonor.Application.Equipment.Models;
using RollForHonor.Domain.Items.Configuration;
using RollForHonor.Domain.Items.Equipment.Models;
using RollForHonor.Domain.Items.Models;
using RollForHonor.Domain.Items.Services;
using RollForHonor.Domain.Stats;

namespace RollForHonor.Application.Tests.Equipment;

/// <summary>Verifies ownership, stat recomposition and atomic publication of paired hand changes.</summary>
public sealed class EquipmentHandRuntimeTests
{
    /// <summary>Dependent offhands remain owned and lose their bonuses atomically when a main hand changes.</summary>
    [Fact]
    public void WeaponChangesReturnOwnedOffHandsWithoutRetainingTheirStats()
    {
        var session = new EquipmentTestSession();
        var bow = Item("bow", ["weapon", "bow"]);
        var sword = Item("sword", ["weapon", "one-handed"]);
        var quiver = Item("quiver", ["offhand", "quiver"], new ItemModifier(new(StatType.AttackModifier), StatOperation.Flat, 2));
        var shield = Item("shield", ["offhand", "shield"], new ItemModifier(new(StatType.Armor), StatOperation.Flat, 4));

        foreach (var item in new[] { bow, sword, quiver, shield })
        {
            session.State.RegisterOwnedItem(session.Owner, item);
        }

        Assert.Equal(EquipmentChangeStatus.IncompatibleSlot, session.Service.EquipItem(session.Owner, quiver.Id, EquipmentSlot.OffHand));
        session.Service.EquipItem(session.Owner, bow.Id, EquipmentSlot.Weapon);
        session.Service.EquipItem(session.Owner, quiver.Id, EquipmentSlot.OffHand);
        Assert.Equal(2, session.State.GetStats(session.Owner).Get(new(StatType.AttackModifier)));
        var before = session.State.ReadEquipment(session.Owner);
        Assert.Equal(EquipmentChangeStatus.IncompatibleSlot, session.Service.EquipItem(session.Owner, shield.Id, EquipmentSlot.OffHand));
        Assert.Equal(before.Version, session.State.ReadEquipment(session.Owner).Version);

        session.Service.EquipItem(session.Owner, sword.Id, EquipmentSlot.Weapon);
        var after = session.State.ReadEquipment(session.Owner);
        Assert.Null(after.Loadout.GetItem(EquipmentSlot.OffHand));
        Assert.Equal(0, session.State.GetStats(session.Owner).Get(new(StatType.AttackModifier)));
        Assert.Same(quiver, after.OwnedItems[quiver.Id]);
        Assert.Equal(EquipmentChangeStatus.Applied, session.Service.EquipItem(session.Owner, shield.Id, EquipmentSlot.OffHand));
        Assert.Equal(6, session.State.GetStats(session.Owner).Get(new(StatType.Armor)));

        session.Service.EquipItem(session.Owner, bow.Id, EquipmentSlot.Weapon);
        Assert.Null(session.State.ReadEquipment(session.Owner).Loadout.GetItem(EquipmentSlot.OffHand));
        Assert.Equal(2, session.State.GetStats(session.Owner).Get(new(StatType.Armor)));
        Assert.Equal(4, session.State.ReadEquipment(session.Owner).OwnedItems.Count);
    }

    /// <summary>A direct proposal cannot bypass hand compatibility at the storage boundary.</summary>
    [Fact]
    public void ForgedIncompatibleHandPairDoesNotPublish()
    {
        var session = new EquipmentTestSession();
        var bow = Item("bow", ["weapon", "bow"]);
        var shield = Item("shield", ["offhand", "shield"]);
        session.State.RegisterOwnedItem(session.Owner, bow);
        session.State.RegisterOwnedItem(session.Owner, shield);
        var before = session.State.ReadEquipment(session.Owner);
        var loadout = new EquipmentLoadout([new(EquipmentSlot.Weapon, bow), new(EquipmentSlot.OffHand, shield)]);

        Assert.Throws<InvalidOperationException>(() => session.State.TryCommitEquipment(new(before.Version, session.Owner, loadout)));
        Assert.Equal(before.Version, session.State.ReadEquipment(session.Owner).Version);
        Assert.Empty(session.State.ReadEquipment(session.Owner).Loadout.Slots);
    }

    private static ItemInstance Item(string id, string[] tags, params ItemModifier[] modifiers) => new ItemFactory(ItemRarityRules.Default, ItemStatRules.Default)
        .Create(id, new ItemDefinition(id, id, tags, modifiers), ItemRarity.Common);
}
