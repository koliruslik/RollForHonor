using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Chickensoft.GoDotTest;
using Godot;
using RollForHonor.Application.Combat.Models;
using RollForHonor.Application.Combat.Services;
using RollForHonor.Domain.Combat.Attacks.Models;
using RollForHonor.Domain.Combat.Damage.Configuration;
using RollForHonor.Domain.Combat.Damage.Models;
using RollForHonor.Domain.Combat.Effects.Models;
using RollForHonor.Domain.Combat.Resolution.Services.Steps;
using RollForHonor.Domain.Dice.Models;
using RollForHonor.Domain.Dice.Services;
using RollForHonor.Domain.Items.Affixes.Models;
using RollForHonor.Domain.Items.Affixes.Services;
using RollForHonor.Domain.Resources.Services;
using RollForHonor.Domain.Stats;
using RollForHonor.Godot.Arena;
using RollForHonor.Godot.Content.Loot;
using RollForHonor.Godot.Gameplay.Combat;
using RollForHonor.Godot.Gameplay.Targeting;
using RollForHonor.Godot.UI.Inventory;
using RollForHonor.Godot.UI.Reference;
using Shouldly;

namespace RollForHonor.Godot.Tests;

/// <summary>Checks live poison content, localized inspection and actual projectile-to-scheduler integration.</summary>
public sealed class GodotPoisonTest : TestClass
{
    private GameBootstrap Runtime => TestScene.GetTree().Root.GetChildren()
        .OfType<GameBootstrap>()
        .Single();

    /// <summary>Creates poison checks in the native engine process.</summary>
    /// <param name="testScene">The engine test host.</param>
    public GodotPoisonTest(Node testScene) : base(testScene) { }

    /// <summary>The single live poison family captures exactly the seven approved chance/damage pairs.</summary>
    [Test]
    public void CatalogAndInspectionUseApprovedPoisonValues()
    {
        var family = GameLootCatalog.Affixes.Single(value => value.GetProperty("id").GetString() == "poison-on-hit");
        GameLootCatalog.Affixes.Any(value => new[] { "added-poison", "dice-poison", "increased-poison", "resist-poison" }
            .Contains(value.GetProperty("id").GetString())).ShouldBeFalse();
        var definition = new AffixDefinition("poison-on-hit", "poison-on-hit", 40,
            family.GetProperty("tiers").EnumerateObject()
                .Select(tier => new AffixTier(
                tier.Value.GetProperty("tier_id").GetString(), 0, GameLootCatalog.ReadRanges(family, tier.Value))));

        for (var tier = 1; tier <= 7; tier++)
        {
            var affix = new AffixRoller().Roll(Guid.NewGuid().ToString(), definition, $"T{tier}", new HitDice());
            var chance = affix.Modifiers.Single(modifier => modifier.Key.Type == StatType.PoisonChance);
            var damage = affix.Modifiers.Single(modifier => modifier.Key.Type == StatType.PoisonDice);
            chance.Amount.ShouldBe((45 - tier * 5) / 100m);
            damage.Amount.ShouldBe(tier <= 2 ? 3 : tier <= 4 ? 2 : 1);
            ItemPresentation.Modifier(chance).ShouldContain($"{45 - tier * 5}%");
            ItemPresentation.Modifier(damage).ShouldContain($"{damage.Amount}d4");
        }

        var reference = LootReference.Create().Single(entry => entry.Id == "loot.affixes").Table;
        reference.Tiers[0].Rows.Single(row => row.Id == "poison-on-hit").Cells[2].ShouldContain("10%");
        reference.Tiers[6].Rows.Single(row => row.Id == "poison-on-hit").Cells[2].ShouldContain("40%");
        var previous = TranslationServer.GetLocale();
        TranslationServer.SetLocale("uk");

        try
        {
            ItemPresentation.AffixName("poison-on-hit").ShouldBe("Шанс отруїти при влучанні");
            ItemPresentation.Modifier(new(new(StatType.PoisonChance), StatOperation.Flat, 0.4m)).ShouldContain("шанс отруїти");
            ReferenceCatalog.Create().Any(entry => entry.Id is "damage.Poison" or "damage.Pure").ShouldBeFalse();
        }
        finally
        {
            TranslationServer.SetLocale(previous);
        }
    }

    /// <summary>Real projectile hits add independent stacks, pause preserves their clock, and the native arena expires them after five ticks.</summary>
    /// <returns>Completes after two physical contacts, a paused interval and natural stack expiry.</returns>
    [Test]
    public async Task ProjectilePoisonTicksAndExpiresOnGameTime()
    {
        var arena = GD.Load<PackedScene>("res://Arena.tscn").Instantiate<GreyboxArena>();
        Runtime.AddChild(arena);
        arena.Player.Controls.HardwareInput = false;
        arena.Opponent.Controls.ThinkingEnabled = false;
        arena.Player.Position = new(-10, 0.05f, 32);
        arena.Opponent.Position = new(-16, 0.05f, 32);
        var state = Runtime.CombatState;
        var player = arena.Player.CombatantId;
        var target = arena.Opponent.CombatantId;
        arena.Player.GetRequiredComponent<CombatAwareness>().Selection.Select(target);
        var profile = state.ReadEquipment(player).NonEquipmentProfile;
        state.UpdateNonEquipmentStats(player, new(profile.BaseValues
            .With(new(StatType.PoisonChance), 1).With(new(StatType.PoisonDice), 1), profile.Modifiers));
        var targetProfile = state.GetStatProfile(target);
        state.UpdateStats(target, new(targetProfile.BaseValues.With(new(StatType.MaxHealth), 100)
            .With(new(StatType.Armor), 100000).With(new(StatType.Evasion), 0)
            .With(new(StatType.Resistance, DamageType.Fire), 1), targetProfile.Modifiers));
        var prepared = state.Read(target, target).Target;
        state.UpdateCombatant(new(target, 100, prepared.MaxHealth, prepared.Armor, prepared.Evasion,
            prepared.Resistances, prepared.Effects, prepared.Offense, prepared.EvasionRating));
        var impacts = new CombatImpactService(state, new CombatSnapshotBuilder(),
            new BasicAttackPipelineFactory(new DamageMultipliers(0, 0.5m, 1, 2), ratingRules: true, statusRules: RollForHonor.Godot.Content.Combat.StatusEffectContent.Rules), () => new HitDice());
        var attacks = new AttackInitiationService(state, new ResourceCostResolver(new AdditiveResourceCostPolicy()),
            new NoResourceCostModifiers(), impacts, () => Runtime.GameTime);
        var projectile = arena.Player.GetRequiredComponent<ProjectileAttack>();
        var attack = new AttackDefinition("poison-test", new(new(1, 20), 0, NaturalOutcomeShiftRules.Standard,
            [new(new(1, 4), 0, DamageType.Fire)], [], AttackTags.Projectile), TimeSpan.Zero, []);
        projectile.AttackProvider = () => attack;
        projectile.Configure(attacks, state, attack, GD.Load<PackedScene>("res://Gameplay/Projectiles/AirborneProjectile.tscn"));

        try
        {
            await Frames(3);

            for (var stack = 1; stack <= 2; stack++)
            {
                projectile.RequestAttack(arena.Opponent.AimPoint).Attack.ShouldNotBeNull();

                for (var frame = 0; frame < 60 && state.Read(target, target).Target.Effects.Count < stack; frame++)
                {
                    await Frames(1);
                }

                state.Read(target, target).Target.Effects.Count.ShouldBe(stack);
            }

            var applied = state.Read(target, target).Target;
            applied.Health.ShouldBe(100);
            arena.GetNode<RollForHonor.Godot.UI.Hud.ArenaHud>("Presentation")._Process(0);
            var statusRow = arena.GetNode<RollForHonor.Godot.UI.Hud.ArenaHud>("Presentation")
                .GetNode<RollForHonor.Godot.UI.Hud.TargetStatusIcons>("Root/Design/Enemy/Statuses");
            statusRow.GetNode<Button>("Poison").Visible.ShouldBeTrue();
            statusRow.GetNode<Label>("Poison/Count").Text.ShouldBe("2");
            applied.Effects.All(effect => effect.Definition.Id == RollForHonor.Domain.Combat.Effects.Models.StatusEffectIds.Poison).ShouldBeTrue();
            var time = Runtime.GameTime;
            TestScene.GetTree().Paused = true;
            await Frames(5);
            Runtime.GameTime.ShouldBe(time);
            state.Read(target, target).Target.Health.ShouldBe(100);
            TestScene.GetTree().Paused = false;

            for (var frame = 0; frame < 420 && state.Read(target, target).Target.Effects.Count > 0; frame++)
            {
                await Frames(1);
            }

            var after = state.Read(target, target).Target;
            after.Effects.ShouldBeEmpty();
            arena.GetNode<RollForHonor.Godot.UI.Hud.ArenaHud>("Presentation")._Process(0);
            statusRow.GetNode<Button>("Poison").Visible.ShouldBeFalse();
            after.Health.ShouldBeInRange(60, 90);
            await Frames(8);
            state.Read(target, target).Target.Health.ShouldBe(after.Health);
        }
        finally
        {
            TestScene.GetTree().Paused = false;
            arena.Free();
        }
    }

    /// <summary>Actual projectiles apply ignite and bleed, retain captured data on refresh and slow the native movement motor once.</summary>
    /// <returns>Completes after two real impacts and at least one damaging production tick.</returns>
    [Test]
    public async Task ProjectilesRefreshIgniteAndBleedWithoutReplacingDamageOrSlow()
    {
        var arena = GD.Load<PackedScene>("res://Arena.tscn").Instantiate<GreyboxArena>();
        Runtime.AddChild(arena);
        arena.Player.Controls.HardwareInput = false;
        arena.Opponent.Controls.ThinkingEnabled = false;
        arena.Player.Position = new(-10, 0.05f, 32);
        arena.Opponent.Position = new(-16, 0.05f, 32);
        var state = Runtime.CombatState;
        var player = arena.Player.CombatantId;
        var target = arena.Opponent.CombatantId;
        var sourceProfile = state.ReadEquipment(player).NonEquipmentProfile;
        var bases = sourceProfile.BaseValues.With(new(StatType.IgniteChance), 1).With(new(StatType.IgniteDice), 1)
            .With(new(StatType.BleedChance), 1).With(new(StatType.BleedDice), 1);
        state.UpdateNonEquipmentStats(player, new(bases, sourceProfile.Modifiers));
        var targetProfile = state.GetStatProfile(target);
        state.UpdateStats(target, new(targetProfile.BaseValues.With(new(StatType.MaxHealth), 100)
            .With(new(StatType.MovementSpeed), 10).With(new(StatType.Armor), 100000)
            .With(new(StatType.Evasion), 0).With(new(StatType.Resistance, DamageType.Fire), 0.5m), targetProfile.Modifiers));
        var prepared = state.Read(target, target).Target;
        state.UpdateCombatant(new(target, 100, prepared.MaxHealth, prepared.Armor, prepared.Evasion,
            prepared.Resistances, prepared.Effects, prepared.Offense, prepared.EvasionRating));
        var impacts = new CombatImpactService(state, new CombatSnapshotBuilder(),
            new BasicAttackPipelineFactory(new DamageMultipliers(0, 0.5m, 1, 2), true,
                RollForHonor.Godot.Content.Combat.StatusEffectContent.Rules), () => new HitDice());
        var attacks = new AttackInitiationService(state, new ResourceCostResolver(new AdditiveResourceCostPolicy()),
            new NoResourceCostModifiers(), impacts, () => Runtime.GameTime);
        var projectile = arena.Player.GetRequiredComponent<ProjectileAttack>();
        var attack = new AttackDefinition("status-test", new(new(1, 20), 0, NaturalOutcomeShiftRules.Standard,
            [new(new(1, 4), -1, DamageType.Fire)], [], AttackTags.Projectile), TimeSpan.FromSeconds(0.25), []);
        projectile.AttackProvider = () => attack;
        projectile.Configure(attacks, state, attack, GD.Load<PackedScene>("res://Gameplay/Projectiles/AirborneProjectile.tscn"));

        try
        {
            await Frames(3);
            projectile.RequestAttack(arena.Opponent.AimPoint).Attack.AttacksPerSecond.ShouldBe(4);

            for (var frame = 0; frame < 60 && state.Read(target, target).Target.Effects.Count < 2; frame++)
            {
                await Frames(1);
            }

            var original = state.Read(target, target).Target.Effects.ToArray();
            original.Length.ShouldBe(2);
            state.Read(target, target).Target.Health.ShouldBe(100);
            state.GetStats(target).Get(new(StatType.MovementSpeed)).ShouldBe(9);
            state.UpdateNonEquipmentStats(player, new(bases.With(new(StatType.IgniteDice), 3)
                .With(new(StatType.BleedSlowMultiplier), 2), sourceProfile.Modifiers));
            await Frames(20);
            projectile.RequestAttack(arena.Opponent.AimPoint).Status.ShouldBe(AttackStartStatus.Started);
            await Frames(25);
            var refreshed = state.Read(target, target).Target.Effects;
            refreshed.Count.ShouldBe(2);
            refreshed.Select(effect => effect.Id).ShouldBe(original.Select(effect => effect.Id));
            refreshed.All(effect => ((PeriodicDamageEffectDefinition)effect.Definition).Damage.Single().Dice.Count == 1).ShouldBeTrue();
            state.GetStats(target).Get(new(StatType.MovementSpeed)).ShouldBe(9);
            arena.Opponent.GetRequiredComponent<RollForHonor.Godot.Gameplay.Movement.MovementMotor>().Speed.ShouldBe(9);

            for (var frame = 0; frame < 90 && state.Read(target, target).Target.Health == 100; frame++)
            {
                await Frames(1);
            }

            var after = state.Read(target, target).Target;
            after.Health.ShouldBeLessThan(100);
            arena.GetNode<RollForHonor.Godot.UI.Hud.ArenaHud>("Presentation").FindChild("DamageNumbers", true, false)
                .GetChildCount().ShouldBeGreaterThan(0);
            state.UpdateCombatant(new(target, after.Health, after.MaxHealth, after.Armor, after.Evasion,
                after.Resistances, [], after.Offense, after.EvasionRating));
            await Frames(2);
            state.GetStats(target).Get(new(StatType.MovementSpeed)).ShouldBe(10);
            arena.Opponent.GetRequiredComponent<RollForHonor.Godot.Gameplay.Movement.MovementMotor>().Speed.ShouldBe(10);
        }
        finally
        {
            arena.Free();
        }
    }

    private async Task Frames(int count)
    {
        for (var frame = 0; frame < count; frame++)
        {
            await TestScene.ToSignal(TestScene.GetTree(), SceneTree.SignalName.PhysicsFrame);
        }
    }

    private sealed class HitDice : IDiceRoller
    {
        private readonly List<DiceRoll> _rolls = [];

        /// <inheritdoc />
        public DiceTrace Trace => new(_rolls);

        /// <inheritdoc />
        public DiceRoll Roll(DiceFormula formula)
        {
            var results = Enumerable.Repeat(formula.Sides == 20 ? 15 : 1, formula.Count).ToArray();
            var roll = new DiceRoll(formula, results, results.Sum());
            _rolls.Add(roll);
            return roll;
        }
    }
}
