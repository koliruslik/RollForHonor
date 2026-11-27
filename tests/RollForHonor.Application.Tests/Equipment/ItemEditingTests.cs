using RollForHonor.Application.Equipment.Models;
using RollForHonor.Domain.Items.Affixes.Models;
using RollForHonor.Domain.Items.Affixes.Services;
using RollForHonor.Domain.Items.Configuration;
using RollForHonor.Domain.Items.Equipment.Models;
using RollForHonor.Domain.Items.Models;
using RollForHonor.Domain.Items.Services;
using RollForHonor.Domain.Stats;
using RollForHonor.Infrastructure.Dice;

namespace RollForHonor.Application.Tests.Equipment;

/// <summary>Verifies administrative item replacement preserves identity, ownership and atomic equipped-state projection.</summary>
public sealed class ItemEditingTests
{
    /// <summary>Editing equipped affixes replaces the canonical snapshot and derived values together without healing.</summary>
    [Fact]
    public void EquippedEditRecomposesStatsAndRejectsAStaleDraft()
    {
        var session = new EquipmentTestSession();
        var original = EquipmentTestSession.Item("owned", "body-armor");
        session.State.RegisterOwnedItem(session.Owner, original);
        Assert.Equal(EquipmentChangeStatus.Applied, session.Service.EquipItem(session.Owner, original.Id, EquipmentSlot.BodyArmor));
        var replacement = Edited(original, StatType.MaxHealth, 50);
        var before = session.State.Read(session.Owner, session.Owner).Source;

        Assert.True(session.State.TryEditOwnedItem(session.Owner, original, replacement));

        var read = session.State.ReadEquipment(session.Owner);
        Assert.Same(replacement, read.OwnedItems[original.Id]);
        Assert.Same(replacement, read.Loadout.GetItem(EquipmentSlot.BodyArmor));
        Assert.Equal(150, session.State.GetStats(session.Owner).Get(new(StatType.MaxHealth)));
        Assert.Equal(before.Health, session.State.Read(session.Owner, session.Owner).Source.Health);
        var version = read.Version;
        Assert.False(session.State.TryEditOwnedItem(session.Owner, original, Edited(original, StatType.MaxHealth, 80)));
        Assert.Equal(version, session.State.ReadEquipment(session.Owner).Version);
        Assert.Same(replacement, session.State.ReadEquipment(session.Owner).OwnedItems[original.Id]);
    }

    /// <summary>Projection overflow publishes neither item replacement nor changed loadout/stats.</summary>
    [Fact]
    public void InvalidEquippedEditIsAtomic()
    {
        var session = new EquipmentTestSession();
        var original = EquipmentTestSession.Item("owned", "body-armor");
        session.State.RegisterOwnedItem(session.Owner, original);
        session.Service.EquipItem(session.Owner, original.Id, EquipmentSlot.BodyArmor);
        var before = session.State.ReadEquipment(session.Owner);
        var actor = session.State.Read(session.Owner, session.Owner).Source;

        Assert.Throws<OverflowException>(() => session.State.TryEditOwnedItem(session.Owner, original,
            Edited(original, StatType.MaxHealth, int.MaxValue)));

        Assert.Equal(before.Version, session.State.ReadEquipment(session.Owner).Version);
        Assert.Same(original, session.State.ReadEquipment(session.Owner).OwnedItems[original.Id]);
        Assert.Same(before.Loadout, session.State.ReadEquipment(session.Owner).Loadout);
        Assert.Same(actor, session.State.Read(session.Owner, session.Owner).Source);
    }

    /// <summary>An unequipped edit keeps bag identity and does not activate bonuses or accept a changed base.</summary>
    [Fact]
    public void BagEditsDoNotEquipOrPermitIdentityChanges()
    {
        var session = new EquipmentTestSession();
        var original = EquipmentTestSession.Item("owned", "body-armor");
        session.State.RegisterOwnedItem(session.Owner, original);
        var replacement = Edited(original, StatType.MaxHealth, 50);

        Assert.True(session.State.TryEditOwnedItem(session.Owner, original, replacement));
        Assert.Equal(100, session.State.GetStats(session.Owner).Get(new(StatType.MaxHealth)));
        Assert.Empty(session.State.ReadEquipment(session.Owner).Loadout.Slots);
        Assert.Single(session.State.ReadEquipment(session.Owner).OwnedItems);
        Assert.Throws<ArgumentException>(() => session.State.TryEditOwnedItem(session.Owner, replacement,
            EquipmentTestSession.Item("owned", "weapon")));
    }

    private static ItemInstance Edited(ItemInstance original, StatType type, decimal amount)
    {
        var definition = new AffixDefinition("admin", "admin", 1,
            [new("T1", 0, [new(new(type), StatOperation.Flat, amount, amount)])]);
        var affix = new AffixRoller().Roll("affix", definition, "T1", new RandomDiceRoller());
        return new ItemFactory(ItemRarityRules.Default, ItemStatRules.Default)
            .Create(original.Id, original.Base, ItemRarity.Rare, [affix], original.GenerationLevel);
    }
}
