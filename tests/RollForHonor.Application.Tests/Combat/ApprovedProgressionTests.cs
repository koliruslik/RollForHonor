using RollForHonor.Application.Equipment.Models;
using RollForHonor.Application.Equipment.Services;
using RollForHonor.Domain.Combat.State.Models;
using RollForHonor.Domain.Items.Configuration;
using RollForHonor.Domain.Items.Equipment.Models;
using RollForHonor.Domain.Items.Models;
using RollForHonor.Domain.Items.Services;
using RollForHonor.Domain.Stats;
using RollForHonor.Infrastructure.Combat;

namespace RollForHonor.Application.Tests.Combat;

/// <summary>Checks authoritative start selection and requirements without inventing XP or point awards.</summary>
public sealed class ApprovedProgressionTests
{
    /// <summary>Selection grants and equips once without healing, allocating points or giving a hidden kit.</summary>
    [Fact]
    public void StarterSelectionIsAtomicAndCannotBeRepeated()
    {
        var state = new InMemoryCombatState();
        var owner = new CombatantId(Guid.NewGuid());
        state.AddStatCombatant(owner, Profile(1, 0));
        var current = state.Read(owner, owner).Source;
        state.UpdateCombatant(new(owner, 70, current.MaxHealth, current.Armor, current.Evasion, [], []));
        var weapon = Factory().Create("starter", new("bow", "Bow", ["weapon", "bow"], weapon: new(4, 10, [new(new(1, 6), 3, RollForHonor.Domain.Combat.Damage.Models.DamageType.Piercing)])), ItemRarity.Common);

        Assert.True(state.TrySelectStarterWeapon(owner, weapon));
        var after = state.ReadEquipment(owner);
        Assert.Single(after.OwnedItems);
        Assert.Same(weapon, after.Loadout.GetItem(EquipmentSlot.Weapon));
        Assert.Equal(70, state.Read(owner, owner).Source.Health);
        Assert.Equal(0, state.GetStats(owner).Get(new(StatType.Strength)));
        Assert.False(state.TrySelectStarterWeapon(owner, weapon));
        Assert.Equal(after.Version, state.ReadEquipment(owner).Version);
    }

    /// <summary>Both the use case and adapter reject unmet requirements; level changes add no resource stats.</summary>
    [Fact]
    public void RequirementsCannotBeBypassedAndInvalidStatChangesPublishNothing()
    {
        var state = new InMemoryCombatState();
        var owner = new CombatantId(Guid.NewGuid());
        state.AddStatCombatant(owner, Profile(1, 0));
        var item = Factory().Create("required", new("armor", "Armor", ["body-armor"], requirements: new(3, StatType.Strength, 3)), ItemRarity.Common);
        state.RegisterOwnedItem(owner, item);
        var before = state.ReadEquipment(owner);
        var loadout = new EquipmentLoadout([new(EquipmentSlot.BodyArmor, item)]);

        Assert.Equal(EquipmentChangeStatus.RequirementsNotMet, new EquipmentService(state).EquipItem(owner, item.Id, EquipmentSlot.BodyArmor));
        Assert.Equal(EquipmentChangeStatus.RequirementsNotMet, state.TryCommitEquipment(new(before.Version, owner, loadout)));
        Assert.Equal(before.Version, state.ReadEquipment(owner).Version);
        state.UpdateNonEquipmentStats(owner, Profile(3, 3));
        Assert.Equal(100, state.Read(owner, owner).Source.MaxHealth);
        Assert.Equal(EquipmentChangeStatus.Applied, new EquipmentService(state).EquipItem(owner, item.Id, EquipmentSlot.BodyArmor));
        var equipped = state.ReadEquipment(owner);
        Assert.Throws<ArgumentException>(() => state.UpdateNonEquipmentStats(owner, Profile(2, 2)));
        Assert.Equal(equipped.Version, state.ReadEquipment(owner).Version);
        Assert.Throws<ArgumentOutOfRangeException>(() => state.UpdateNonEquipmentStats(owner, Profile(11, 3)));
        Assert.Equal(equipped.Version, state.ReadEquipment(owner).Version);
    }

    private static ItemFactory Factory() => new(ItemRarityRules.Default, ItemStatRules.Default);

    private static StatProfile Profile(int level, int strength) => new(new StatBlock([
        new(new(StatType.MaxHealth), 100), new(new(StatType.MaxMana), 500),
        new(new(StatType.CharacterLevel), level), new(new(StatType.Strength), strength)
    ]));
}
