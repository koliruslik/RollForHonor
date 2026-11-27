using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Chickensoft.GoDotTest;
using Godot;
using RollForHonor.Application.Items.Upgrades;
using RollForHonor.Application.Rewards;
using RollForHonor.Domain.Combat.State.Models;
using RollForHonor.Domain.Items.Configuration;
using RollForHonor.Domain.Items.Equipment.Models;
using RollForHonor.Domain.Items.Models;
using RollForHonor.Domain.Items.Services;
using RollForHonor.Godot.Arena;
using RollForHonor.Godot.Content.Items;
using RollForHonor.Godot.Content.Loot;
using RollForHonor.Godot.UI.Hud;
using RollForHonor.Godot.UI.Inventory;
using RollForHonor.Infrastructure.Dice;
using Shouldly;

namespace RollForHonor.Godot.Tests;

/// <summary>Verifies real upgrade controls, native drops, live generation and localized rendering.</summary>
/// <param name="testScene">The active native test host.</param>
public sealed class GodotItemUpgradeTest(Node testScene) : TestClass(testScene)
{
    /// <summary>The native menu pays exactly 96 gold for the equipped starter's first evolution.</summary>
    /// <returns>Completes after window layout and optional real capture.</returns>
    [Test]
    public async Task NativeButtonsEnhanceAndEvolveEquippedStarter()
    {
        var arena = Create();
        try
        {
            var state = arena.GetParent<GameBootstrap>().CombatState;
            var owner = arena.Player.CombatantId;
            Fund(arena, 96);
            var original = state.ReadEquipment(owner).Loadout.GetItem(EquipmentSlot.Weapon);
            var hud = arena.GetNode<ArenaHud>("Presentation");
            hud.ToggleUpgrades();
            var window = hud.GetNode<ItemUpgradeWindow>("Root/Design/ItemUpgrades");
            await Frames(2);
            window.Visible.ShouldBeTrue();
            var drop = window.GetNode<ItemCell>("UpgradeDrop");
            drop.ItemId.ShouldBeEmpty();
            FindButton(window, "Enhance").Disabled.ShouldBeTrue();
            state.ReadEquipment(owner).Loadout.GetItem(EquipmentSlot.Weapon).ShouldBeSameAs(original);
            await Capture("empty-slot-en.png");
            var data = new global::Godot.Collections.Dictionary { ["rfh-item-id"] = original.Id };
            drop._CanDropData(Vector2.One, data).ShouldBeTrue();
            drop._DropData(Vector2.One, data);
            var button = FindButton(window, "Enhance");
            for (var index = 0; index < 3; index++)
            {
                button.Disabled.ShouldBeFalse();
                button.EmitSignal(Button.SignalName.Pressed);
            }

            state.ReadGoldWallet(owner).Balance.ShouldBe(35);
            state.ReadEquipment(owner).Loadout.GetItem(EquipmentSlot.Weapon).Progression.Enhancement.ShouldBe(3);
            await Capture("enhancement-plus3-en.png");
            button.Disabled.ShouldBeTrue();
            var evolution = FindButton(window, "Evolve");
            evolution.Visible.ShouldBeTrue();
            evolution.Disabled.ShouldBeFalse();
            evolution.EmitSignal(Button.SignalName.Pressed);
            var evolved = state.ReadEquipment(owner).Loadout.GetItem(EquipmentSlot.Weapon);
            evolved.Rarity.ShouldBe(ItemRarity.Uncommon);
            evolved.Progression.Enhancement.ShouldBe(0);
            evolved.Progression.RarityMultiplier.ShouldBe(1.15m);
            evolved.Base.ShouldBeSameAs(original.Base);
            state.ReadGoldWallet(owner).Balance.ShouldBe(0);
            var attack = arena.Player.GetRequiredComponent<RollForHonor.Godot.Gameplay.Combat.ProjectileAttack>()
                .RequestAttack(arena.Player.GlobalPosition + new Vector3(0, 1, -4));
            attack.Attack.ShouldNotBeNull();
            attack.Attack.Attack.Damage.Single().Multiplier.ShouldBe(1.15m);
            await Frames(2);
            await Capture("evolution-uncommon-en.png");
            TranslationServer.SetLocale("uk");
            await Frames(2);
            FindButton(window, "Enhance").Text.ShouldStartWith("Посилити");
            await Capture("evolution-uncommon-uk.png");
            window.GetNode<Button>("ClearSlot").EmitSignal(Button.SignalName.Pressed);
            drop.ItemId.ShouldBeEmpty();
            FindButton(window, "Enhance").Disabled.ShouldBeTrue();
            state.ReadEquipment(owner).Loadout.GetItem(EquipmentSlot.Weapon).ShouldBeSameAs(evolved);
            drop._DropData(Vector2.One, data);
            drop.ItemId.ShouldBe(evolved.Id);
            hud.ToggleUpgrades();
            hud.ToggleUpgrades();
            await Frames(2);
            drop.ItemId.ShouldBeEmpty();
            FindButton(window, "Enhance").Disabled.ShouldBeTrue();
            state.ReadGoldWallet(owner).Balance.ShouldBe(0);
            await Capture("empty-slot-uk.png");
        }
        finally
        {
            TranslationServer.SetLocale("en");
            arena.Free();
        }
    }

    /// <summary>Live pools support paid addition, complete locked reroll and current-tier gates for every released base.</summary>
    [Test]
    public void LivePoolsHonorCapacityLocksAndCurrentLuck()
    {
        var arena = Create();
        try
        {
            var state = arena.GetParent<GameBootstrap>().CombatState;
            var owner = arena.Player.CombatantId;
            Fund(arena, 100000);
            var service = new ItemUpgradeService(state, ItemUpgradeContent.Rules,
                new ItemFactory(ItemRuleContent.LoadRarityRules(), ItemStatRules.Default), new ItemUpgradeContent(),
                () => new RandomDiceRoller(new Random(174)));
            foreach (var entry in GameLootCatalog.Bases.Where(entry => entry.TryGetProperty("released", out var released) && released.GetBoolean()))
            {
                var item = GameLootCatalog.CreateItem(entry.GetProperty("id").GetString(), Guid.NewGuid().ToString("N"),
                    ItemRarity.Rare, new RandomDiceRoller(new Random(21)), occupancyPercent: 100);
                state.RegisterOwnedItem(owner, item);
                var locked = item.Affixes.Take(2).ToArray();
                var quote = service.Quote(owner, item, ItemUpgradeOperation.Reroll, locked.Select(affix => affix.Id));
                quote.Status.ShouldBe(ItemUpgradeStatus.Ready);
                quote.Cost.Amount.ShouldBe(189);
                service.Execute(quote).ShouldBe(ItemUpgradeStatus.Applied);
                quote.Result.Affixes.Take(2).SequenceEqual(locked).ShouldBeTrue();
                quote.Result.Affixes.Select(affix => affix.FamilyId).Distinct().Count().ShouldBe(4);
                quote.Result.Affixes.All(affix => affix.TierId != "T0").ShouldBeTrue();
                service.Quote(owner, quote.Result, ItemUpgradeOperation.AddAffix).Status.ShouldBe(ItemUpgradeStatus.Unavailable);
            }

            var empty = GameLootCatalog.CreateItem("dexterity-weapon-travel", "empty-crafted", ItemRarity.Uncommon,
                new RandomDiceRoller(), occupancyPercent: 0);
            state.RegisterOwnedItem(owner, empty);
            var add = service.Quote(owner, empty, ItemUpgradeOperation.AddAffix);
            service.Execute(add).ShouldBe(ItemUpgradeStatus.Applied);
            add.Result.Affixes.Count.ShouldBe(1);
            add.Result.Progression.ShouldBe(empty.Progression);
        }
        finally
        {
            arena.Free();
        }
    }

    /// <summary>All glow thresholds are independent of rarity and prices reproduce the agreed whole-gold table.</summary>
    [Test]
    public void AuthoredRulesAndGlowMatchThePlan()
    {
        ItemUpgradeContent.Rules.RerollPrice(4, 3).ShouldBe(284);
        ItemUpgradeContent.Rules.AdditionPrice(0).ShouldBe(384);
        Enumerable.Range(1, 10).Select(ItemUpgradeContent.Rules.EnhancementPrice).Sum().ShouldBe(1013);
        ItemPresentation.EnhancementGlow(2).A.ShouldBe(0);
        ItemPresentation.EnhancementGlow(3).ShouldBe(new Color("8fb697"));
        ItemPresentation.EnhancementGlow(5).ShouldBe(new Color("78a9c9"));
        ItemPresentation.EnhancementGlow(7).ShouldBe(new Color("b39bd0"));
        ItemPresentation.EnhancementGlow(10).ShouldBe(new Color("dfb76b"));
    }

    /// <summary>Native lock toggles update the complete reroll price and preserve their actual affix snapshots.</summary>
    /// <returns>Completes after optional native menu capture.</returns>
    [Test]
    public async Task NativeLocksControlPaymentAndPreserveAffixes()
    {
        var arena = Create();
        try
        {
            var state = arena.GetParent<GameBootstrap>().CombatState;
            var owner = arena.Player.CombatantId;
            Fund(arena, 5000);
            var item = GameLootCatalog.CreateItem("dexterity-weapon-travel", "reroll-ui", ItemRarity.Rare,
                new RandomDiceRoller(new Random(81)), occupancyPercent: 100);
            state.RegisterOwnedItem(owner, item);
            var hud = arena.GetNode<ArenaHud>("Presentation");
            hud.ToggleUpgrades();
            var window = hud.GetNode<ItemUpgradeWindow>("Root/Design/ItemUpgrades");
            window.SelectItem(item.Id);
            for (var index = 0; index < 2; index++)
            {
                var toggle = ((VBoxContainer)window.FindChild("AffixRows", true, false)).GetChild<HBoxContainer>(index).GetNode<Button>("Lock");
                toggle.GetNode<TextureRect>("LockIcon").Modulate.A.ShouldBe(0.2f);
                toggle.ButtonPressed = true;
            }

            var lockedIcon = ((VBoxContainer)window.FindChild("AffixRows", true, false)).GetChild<HBoxContainer>(0)
                .GetNode<Button>("Lock").GetNode<TextureRect>("LockIcon");
            lockedIcon.Modulate.ShouldBe(RollForHonor.Godot.UI.Common.HonorTheme.Brass);

            var button = FindButton(window, "Reroll");
            button.Text.ShouldContain("189");
            await Frames(2);
            await Capture("reroll-locked-en.png");
            button.EmitSignal(Button.SignalName.Pressed);
            state.ReadGoldWallet(owner).Balance.ShouldBe(4811);
            var changed = state.ReadEquipment(owner).OwnedItems[item.Id];
            changed.Affixes[0].ShouldBeSameAs(item.Affixes[0]);
            changed.Affixes[1].ShouldBeSameAs(item.Affixes[1]);
            ((VBoxContainer)window.FindChild("AffixRows", true, false)).GetChildren().OfType<Button>().ShouldBeEmpty();
        }
        finally
        {
            arena.Free();
        }
    }

    /// <summary>Clicking free affix rows generates exactly one affix per payment and locks disable the shared reroll.</summary>
    /// <returns>Completes after the native card has been rendered.</returns>
    [Test]
    public async Task EmptyAffixRowsGenerateWithinTheItemProperties()
    {
        var arena = Create();
        try
        {
            var state = arena.GetParent<GameBootstrap>().CombatState;
            var owner = arena.Player.CombatantId;
            Fund(arena, 2000);
            var item = GameLootCatalog.CreateItem("dexterity-weapon-travel", "inline-affixes", ItemRarity.Uncommon,
                new RandomDiceRoller(new Random(31)), occupancyPercent: 0);
            state.RegisterOwnedItem(owner, item);
            var hud = arena.GetNode<ArenaHud>("Presentation");
            hud.ToggleUpgrades();
            var window = hud.GetNode<ItemUpgradeWindow>("Root/Design/ItemUpgrades");
            window.SelectItem(item.Id);
            var rows = (VBoxContainer)window.FindChild("AffixRows", true, false);
            rows.GetChildren().OfType<Button>().Count().ShouldBe(2);
            var properties = ((VBoxContainer)window.FindChild("BaseProperties", true, false)).GetChildren()
                .OfType<RichTextLabel>()
                .Select(label => label.GetParsedText())
                .ToArray();
            properties.Any(text => text.Contains("Piercing damage") && text.Contains("5–11")).ShouldBeTrue();
            properties.All(text => !text.Contains('×') && !text.Contains("1d6")).ShouldBeTrue();
            Input.ActionPress("rfh_details");
            try
            {
                window._Process(0);
                ((VBoxContainer)window.FindChild("BaseProperties", true, false)).GetChildren()
                    .OfType<RichTextLabel>()
                    .Any(label => label.GetParsedText().Contains("(1d6 + 3) ×1.15")).ShouldBeTrue();
                await Frames(2);
                await Capture("damage-formula-alt-en.png");
            }
            finally
            {
                Input.ActionRelease("rfh_details");
                window._Process(0);
            }
            await Frames(2);
            await Capture("inline-empty-affixes-en.png");
            FindButton(window, "AddAffix0").EmitSignal(Button.SignalName.Pressed);
            state.ReadGoldWallet(owner).Balance.ShouldBe(1616);
            var first = state.ReadEquipment(owner).OwnedItems[item.Id].Affixes.Single();
            rows.GetChild<HBoxContainer>(0).GetNode<Button>("Lock").ButtonPressed = true;
            FindButton(window, "Reroll").Disabled.ShouldBeTrue();
            await Frames(2);
            await Capture("inline-affix-lock-en.png");
            FindButton(window, "AddAffix1").EmitSignal(Button.SignalName.Pressed);
            var changed = state.ReadEquipment(owner).OwnedItems[item.Id];
            changed.Affixes.Count.ShouldBe(2);
            changed.Affixes[0].ShouldBeSameAs(first);
            state.ReadGoldWallet(owner).Balance.ShouldBe(1232);
            rows.GetChildren().OfType<Button>().ShouldBeEmpty();
        }
        finally
        {
            arena.Free();
        }
    }

    /// <summary>Every rarity locks enhancement at its own cap and exposes evolution only for nonfinal rarities.</summary>
    /// <returns>Completes after checking a rendered full Legendary affix list.</returns>
    [Test]
    public async Task RarityCapsAndFullAffixListsUseTheSameCard()
    {
        var arena = Create();
        try
        {
            var state = arena.GetParent<GameBootstrap>().CombatState;
            var owner = arena.Player.CombatantId;
            Fund(arena, 5000);
            var hud = arena.GetNode<ArenaHud>("Presentation");
            hud.ToggleUpgrades();
            var window = hud.GetNode<ItemUpgradeWindow>("Root/Design/ItemUpgrades");
            var factory = new ItemFactory(ItemRuleContent.LoadRarityRules(), ItemStatRules.Default);
            foreach (var rarity in Enum.GetValues<ItemRarity>())
            {
                var sample = GameLootCatalog.CreateItem("dexterity-weapon-travel", $"cap-{rarity}", rarity,
                    new RandomDiceRoller(new Random(37)), occupancyPercent: 100);
                var item = factory.Create(sample.Id, sample.Base, rarity, sample.Affixes, sample.GenerationLevel,
                    new(ItemUpgradeContent.Rules.EnhancementLimit(rarity), sample.Progression.RarityMultiplier));
                state.RegisterOwnedItem(owner, item);
                window.SelectItem(item.Id);
                FindButton(window, "Enhance").Disabled.ShouldBeTrue();
                FindButton(window, "Evolve").Visible.ShouldBe(rarity != ItemRarity.Legendary);
                if (rarity == ItemRarity.Legendary)
                {
                    var scroll = window.GetNode<ScrollContainer>("ItemProperties");
                    await Frames(2);
                    scroll.ScrollVertical = 10000;
                    await Frames(2);
                    var reroll = FindButton(window, "Reroll");
                    scroll.GetGlobalRect().Intersects(reroll.GetGlobalRect()).ShouldBeTrue();
                    TranslationServer.SetLocale("uk");
                    await Frames(2);
                    await Capture("inline-legendary-uk.png");
                }
            }
        }
        finally
        {
            TranslationServer.SetLocale("en");
            arena.Free();
        }
    }

    /// <summary>Readable damage endpoints use the same base rounding and post-scaling flat addition as actual rolls.</summary>
    [Test]
    public void DamageRangesMatchActualRollEndpointsAndLocalization()
    {
        var previous = TranslationServer.GetLocale();
        try
        {
            TranslationServer.SetLocale("en");
            var component = new RollForHonor.Domain.Combat.Damage.Models.DamageComponent(new(1, 6), 3,
                RollForHonor.Domain.Combat.Damage.Models.DamageType.Fire, 1.15m);
            var low = new RollForHonor.Domain.Combat.Damage.Models.DamageComponentRoll(component, new(component.Dice, [1], 1)).Amount;
            var high = new RollForHonor.Domain.Combat.Damage.Models.DamageComponentRoll(component, new(component.Dice, [6], 6)).Amount;
            ItemPresentation.WeaponDamageRange(component).ShouldBe($"Fire damage  {low}–{high}");
            ItemPresentation.WeaponDamageRange(component, 2, false).ShouldBe("7–13");
            ItemPresentation.WeaponDamageFormula(component, 2).ShouldBe("(1d6 + 3) ×1.15 + 2 [Fire]");
            ItemPresentation.WeaponDamageFormula(new(component.Dice, 3, component.Type), 2).ShouldBe("1d6 + 5 [Fire]");
            var clamped = new RollForHonor.Domain.Combat.Damage.Models.DamageComponent(new(2, 6), -3,
                RollForHonor.Domain.Combat.Damage.Models.DamageType.Fire, 1.15m);
            ItemPresentation.WeaponDamageRange(clamped, includeType: false).ShouldBe("0–11");
            TranslationServer.SetLocale("uk");
            ItemPresentation.WeaponDamageRange(component).ShouldBe("Вогняна шкода  5–11");
        }
        finally
        {
            TranslationServer.SetLocale(previous);
        }
    }

    private static Button FindButton(Node window, string name) => (Button)window.FindChild(name, true, false);

    private GreyboxArena Create()
    {
        var runtime = TestScene.GetTree().Root.GetChildren().OfType<GameBootstrap>().Single();
        var arena = GD.Load<PackedScene>("res://Arena/RoundArena.tscn").Instantiate<GreyboxArena>();
        runtime.AddChild(arena);
        arena.SelectStarterWeapon("dexterity-weapon-travel").ShouldBeTrue();
        arena.RoundsEnabled = false;
        arena.Player.Controls.HardwareInput = false;
        arena.Opponent.Controls.ThinkingEnabled = false;
        return arena;
    }

    private static void Fund(GreyboxArena arena, long amount)
    {
        var state = arena.GetParent<GameBootstrap>().CombatState;
        var source = new CombatantId(Guid.NewGuid());
        state.AddCombatant(new(source, 0, 1, 0, 0, [], []));
        state.CaptureGoldReward(source, amount);
        new GoldPickupService(state).Collect(arena.Player.CombatantId, source).ShouldBe(GoldPickupStatus.Collected);
    }

    private async Task Frames(int count)
    {
        for (var index = 0; index < count; index++)
        {
            await TestScene.ToSignal(TestScene.GetTree(), SceneTree.SignalName.PhysicsFrame);
        }
    }

    private async Task Capture(string name)
    {
        var directory = OS.GetEnvironment("RFH_UPGRADE_CAPTURE_DIRECTORY");
        if (!string.IsNullOrEmpty(directory))
        {
            await TestScene.ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
            TestScene.GetViewport().GetTexture().GetImage().SavePng(Path.Combine(directory, name)).ShouldBe(Error.Ok);
        }
    }
}
