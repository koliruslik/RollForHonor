using RollForHonor.Application.Combat.Models;
using RollForHonor.Domain.Combat.Damage.Models;
using RollForHonor.Domain.Combat.Effects.Models;
using RollForHonor.Domain.Combat.Resolution.Models;
using RollForHonor.Domain.Combat.State.Models;
using RollForHonor.Domain.Items.Equipment.Models;
using RollForHonor.Domain.Items.Models;
using RollForHonor.Domain.Stats;

namespace RollForHonor.Application.Tests.Equipment;

/// <summary>Verifies equipment affects real impacts while preserving launch spending, cooldowns, effects and current resources.</summary>
public sealed class EquipmentCombatTests
{
    /// <summary>Impacts from an existing attack use current source bonuses and current target resistances after equipment changes.</summary>
    [Fact]
    public void InFlightAttackUsesFreshEquipmentWithoutRepeatingLaunchCosts()
    {
        var session = new EquipmentTestSession();
        var weapon = EquipmentTestSession.Item("weapon", "weapon",
            new ItemModifier(new StatKey(StatType.DamageMultiplier), StatOperation.More, 1),
            new ItemModifier(new StatKey(StatType.AttackModifier), StatOperation.Flat, 3));
        var ring = EquipmentTestSession.Item("ring", "ring",
            new ItemModifier(new StatKey(StatType.Resistance, DamageType.Fire), StatOperation.Flat, 0.5m));
        session.State.RegisterOwnedItem(session.Owner, weapon);
        session.State.RegisterOwnedItem(session.Other, ring);
        var starts = session.CreateStarts();
        var definition = EquipmentTestSession.Shot();
        var request = Guid.NewGuid();
        var attack = starts.Start(request, session.Owner, definition).Attack!;
        var historical = session.State.Read(session.Owner, session.Other);

        session.Service.EquipItem(session.Owner, weapon.Id, EquipmentSlot.Weapon);
        var first = attack.ResolveContact(session.Other).Impact!;
        Assert.True(first.IsCommitted);
        Assert.Equal(3, Assert.IsType<ResolvedCombat>(first.Outcome).Resolution.Result.Target.AttackRoll!.Modifier);
        Assert.Equal(80, session.State.Read(session.Owner, session.Other).Target.Health);
        session.Service.EquipItem(session.Other, ring.Id, EquipmentSlot.RingRight);
        Assert.True(attack.ResolveContact(session.Other).Impact!.IsCommitted);
        Assert.Equal(70, session.State.Read(session.Owner, session.Other).Target.Health);
        session.Service.UnequipItem(session.Owner, EquipmentSlot.Weapon);
        Assert.True(attack.ResolveContact(session.Other).Impact!.IsCommitted);

        Assert.Equal(65, session.State.Read(session.Owner, session.Other).Target.Health);
        Assert.Equal(100, historical.Target.Health);
        Assert.Equal(1, historical.Source.Offense.DamageMultiplier(DamageType.Fire));
        Assert.Equal(AttackStartStatus.AlreadyStarted, starts.Start(request, session.Owner, definition).Status);
        Assert.Equal(AttackStartStatus.OnCooldown, starts.Start(Guid.NewGuid(), session.Owner, definition).Status);
        Assert.Equal(80, session.State.ReadForStart(session.Owner, "shot", request).Resources.Single().Amount);
        Assert.Equal(TimeSpan.FromSeconds(2), session.State.ReadForStart(session.Owner, "shot", request).ReadyAt);
    }

    /// <summary>Increasing maxima never refills current HP/mana; removing equipment clamps downward and preserves active effects.</summary>
    [Fact]
    public void MaximumChangesPreserveCurrentResourcesAndEffects()
    {
        var session = new EquipmentTestSession();
        var item = EquipmentTestSession.Item("vitality", "amulet",
            new ItemModifier(new StatKey(StatType.MaxHealth), StatOperation.Flat, 100),
            new ItemModifier(new StatKey(StatType.MaxMana), StatOperation.Flat, 100));
        session.State.RegisterOwnedItem(session.Owner, item);
        var effect = new CombatEffectSnapshot(new EffectInstanceId(Guid.NewGuid()),
            new PeriodicDamageEffectDefinition(new EffectDefinitionId(Guid.NewGuid()), [new(new(1, 2), 0, DamageType.Fire)]));
        var actor = session.State.Read(session.Owner, session.Owner).Source;
        session.State.UpdateCombatant(new CombatantSnapshot(actor.Id, 70, actor.MaxHealth, actor.Armor, actor.Evasion, [], [effect]));
        session.CreateStarts().Start(Guid.NewGuid(), session.Owner, EquipmentTestSession.Shot());

        session.Service.EquipItem(session.Owner, item.Id, EquipmentSlot.Amulet);

        Assert.Equal(200, session.State.Read(session.Owner, session.Owner).Source.MaxHealth);
        Assert.Equal(70, session.State.Read(session.Owner, session.Owner).Source.Health);
        Assert.Equal(80, session.State.ReadForStart(session.Owner, "shot", Guid.NewGuid()).Resources.Single().Amount);
        session.State.UpdateNonEquipmentStats(session.Owner, new StatProfile(EquipmentTestSession.Profile().BaseValues
            .With(new StatKey(StatType.MaxHealth), 50)
            .With(new StatKey(StatType.MaxMana), 30)));
        session.Service.UnequipItem(session.Owner, EquipmentSlot.Amulet);

        Assert.Equal(50, session.State.Read(session.Owner, session.Owner).Source.Health);
        Assert.Equal(30, session.State.ReadForStart(session.Owner, "shot", Guid.NewGuid()).Resources.Single().Amount);
        Assert.Same(effect, Assert.Single(session.State.Read(session.Owner, session.Owner).Source.Effects));
        session.Service.EquipItem(session.Owner, item.Id, EquipmentSlot.Amulet);
        Assert.Equal(50, session.State.Read(session.Owner, session.Owner).Source.Health);
        Assert.Equal(30, session.State.ReadForStart(session.Owner, "shot", Guid.NewGuid()).Resources.Single().Amount);
    }
}
