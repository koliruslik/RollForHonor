using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Chickensoft.GoDotTest;
using Godot;
using RollForHonor.Application.Equipment.Models;
using RollForHonor.Application.Equipment.Services;
using RollForHonor.Domain.Items.Equipment.Models;
using RollForHonor.Domain.Combat.Effects.Models;
using RollForHonor.Domain.Items.Models;
using RollForHonor.Domain.Stats;
using RollForHonor.Godot.Arena;
using RollForHonor.Godot.Content.Loot;
using RollForHonor.Godot.UI.Admin;
using RollForHonor.Godot.UI.Common;
using RollForHonor.Godot.UI.Hud;
using RollForHonor.Godot.UI.Inventory;
using Shouldly;

namespace RollForHonor.Godot.Tests;

/// <summary>Verifies configurable creation, native inventory-to-editor drops and compact localized legendary cards.</summary>
public sealed class GodotAdminItemTest : TestClass
{
    private GameBootstrap Runtime => TestScene.GetTree().Root.GetChildren()
        .OfType<GameBootstrap>()
        .Single();

    /// <summary>Creates native admin-item checks.</summary>
    /// <param name="testScene">The running engine test host.</param>
    public GodotAdminItemTest(Node testScene) : base(testScene) { }

    /// <summary>Creation honors explicit affixes, quality and level gates and rejects unavailable or duplicate families.</summary>
    [Test]
    public void ConfiguredCreationUsesAvailableFamiliesAndExplicitQuality()
    {
        var arena = Create();

        try
        {
            var hud = arena.GetNode<ArenaHud>("Presentation");
            var admin = (AdminPanel)hud.FindChild("Admin", true, false);
            admin.Open();
            var builder = admin.GetNode<AdminItemEditor>("Panel/Tabs/Items/ItemBuilder");
            Set(builder.GetNode<OptionButton>("Rarity"), (int)ItemRarity.Legendary);
            builder.GetNode<SpinBox>("ItemLevel").Value = 70;
            var row = builder.GetNode<Control>("AffixScroll/AffixRows/Affix0");
            Set(row.GetNode<OptionButton>("Family"), Find(row.GetNode<OptionButton>("Family"), ItemPresentation.AffixName("added-fire")));
            row.GetNode<SpinBox>("Quality").Value = 25;
            builder.GetNode<Button>("Apply").EmitSignal(Button.SignalName.Pressed);
            var item = Runtime.CombatState.ReadEquipment(arena.Player.CombatantId).OwnedItems.Values.Single(value => value.Rarity == ItemRarity.Legendary);
            item.GenerationLevel.ShouldBe(70);
            var affix = item.Affixes.Single();
            affix.DefinitionId.ShouldBe("added-fire");
            affix.TierId.ShouldBe("T1");
            affix.EffectiveQualityRoll.ShouldBe(25);
            var fresh = AdminItemCatalog.Empty("dexterity-weapon-travel", Guid.NewGuid().ToString(), ItemRarity.Legendary, 1);
            AdminItemCatalog.Families(fresh.Base, 1).Any(value => value.GetProperty("id").GetString() == "life-leech").ShouldBeFalse();
            Should.Throw<ArgumentException>(() => AdminItemCatalog.Build(fresh, ItemRarity.Legendary, 1,
                [new("added-fire", "T1", 100)]));
            Should.Throw<ArgumentException>(() => AdminItemCatalog.Build(fresh, ItemRarity.Legendary, 1,
                [new("poison-on-hit", "T7", 100), new("poison-on-hit", "T7", 100)]));
            Should.Throw<ArgumentException>(() => AdminItemCatalog.Build(fresh, ItemRarity.Common, 1,
                [new("poison-on-hit", "T7", 100)]));
        }
        finally
        {
            arena.Free();
        }
    }

    /// <summary>All seven production status tiers and an exclusive guaranteed T0 can be selected at administrative item level one.</summary>
    /// <returns>Completes after the native editor layout and optional rendered capture.</returns>
    [Test]
    public async Task AdministrativeStatusesExposeEveryTierAndGuaranteeT0()
    {
        var arena = Create();

        try
        {
            var admin = (AdminPanel)arena.GetNode<ArenaHud>("Presentation").FindChild("Admin", true, false);
            admin.Open();
            var builder = admin.GetNode<AdminItemEditor>("Panel/Tabs/Items/ItemBuilder");
            Set(builder.GetNode<OptionButton>("Rarity"), (int)ItemRarity.Legendary);
            builder.GetNode<SpinBox>("ItemLevel").Value = 1;
            admin.GetNode<TabContainer>("Panel/Tabs").CurrentTab = 1;
            var ids = new[] { "poison-on-hit", "ignite-on-hit", "bleed-on-hit" };

            for (var index = 0; index < ids.Length; index++)
            {
                var row = builder.GetNode<Control>($"AffixScroll/AffixRows/Affix{index}");
                var families = row.GetNode<OptionButton>("Family");
                Set(families, Find(families, ItemPresentation.AffixName(ids[index])));
                var tiers = row.GetNode<OptionButton>("Tier");
                Enumerable.Range(0, tiers.ItemCount)
                    .Select(tiers.GetItemText)
                    .ShouldBe(["T1", "T2", "T3", "T4", "T5", "T6", "T7", "T0"]);
                Set(tiers, Find(tiers, "T0"));
            }

            await Frames(2);
            await Capture("admin-status-t0.png");
            builder.GetNode<Button>("Apply").EmitSignal(Button.SignalName.Pressed);
            var state = Runtime.CombatState;
            var owner = arena.Player.CombatantId;
            var item = state.ReadEquipment(owner).OwnedItems.Values.Single(value => value.Rarity == ItemRarity.Legendary);
            item.Affixes.All(affix => affix.TierId == "T0").ShouldBeTrue();
            new EquipmentService(state).EquipItem(owner, item.Id, EquipmentSlot.Weapon).ShouldBe(EquipmentChangeStatus.Applied);
            var offense = state.Read(owner, owner).Source.Offense;

            foreach (var kind in Enum.GetValues<StatusEffectKind>())
            {
                var rule = Content.Combat.StatusEffectContent.Rules.Single(value => value.Kind == kind);
                rule.Chance(offense, 100).ShouldBe(1);
                rule.Capture(offense).ShouldNotBeNull();
                state.GetStats(owner).Get(new(rule.Stats.Guaranteed)).ShouldBe(1);
            }

            GameLootCatalog.Affixes.All(family => family.GetProperty("tiers").EnumerateObject()
                .All(tier => tier.Value.GetProperty("tier_id").GetString() != "T0")).ShouldBeTrue();
            var source = AdminItemCatalog.Empty(item.Base.Id, Guid.NewGuid().ToString(), ItemRarity.Legendary, 1);

            foreach (var id in ids)
            {
                foreach (var tier in Enumerable.Range(1, 7))
                {
                    AdminItemCatalog.Build(source, ItemRarity.Legendary, 1, [new(id, $"T{tier}", 100)])
                        .Affixes.Single().TierId.ShouldBe($"T{tier}");
                }
            }

            Should.Throw<ArgumentException>(() => AdminItemCatalog.Build(source, ItemRarity.Legendary, 1,
                [new("added-fire", "T0", 100)]));
        }
        finally
        {
            arena.Free();
        }
    }

    /// <summary>A native drag loads a draft without moving the item; saving updates its canonical equipment and retains untouched rolls.</summary>
    /// <returns>Completes after real drag/drop routing and equipped-stat refresh.</returns>
    [Test]
    public async Task NativeInventoryDropEditsCanonicalOwnedItem()
    {
        var arena = Create();

        try
        {
            var state = Runtime.CombatState;
            var owner = arena.Player.CombatantId;
            var source = AdminItemCatalog.Empty("dexterity-weapon-travel", Guid.NewGuid().ToString(), ItemRarity.Legendary, 70);
            var item = AdminItemCatalog.Build(source, ItemRarity.Legendary, 70,
                [new("poison-on-hit", "T7", 73)]);
            state.RegisterOwnedItem(owner, item);
            var hud = arena.GetNode<ArenaHud>("Presentation");
            hud.ToggleInventory();
            await Frames(2);
            var admin = (AdminPanel)hud.FindChild("Admin", true, false);
            admin.Open();
            admin.MoveToFront();
            admin.GetNode<TabContainer>("Panel/Tabs").CurrentTab = 2;
            await Frames(2);
            var editor = admin.GetNode<AdminItemEditor>("Panel/Tabs/Edit item/ItemEditor");
            var target = editor.GetNode<ItemCell>("DropTarget");
            var bag = (InventoryGridView)hud.FindChild("Grid", true, false);
            var cell = bag.GetChildren()
                .OfType<ItemCell>()
                .Single(value => value.ItemId == item.Id);
            var payload = new global::Godot.Collections.Dictionary { ["rfh-item-id"] = item.Id };
            cell.ForceDrag(payload, new Control { CustomMinimumSize = new(40, 40) });
            target._CanDropData(Vector2.Zero, payload).ShouldBeTrue();
            TestScene.GetViewport().GuiIsDragging().ShouldBeTrue();
            var point = target.GetGlobalTransformWithCanvas() * (target.Size / 2);
            TestScene.GetViewport().PushInput(new InputEventMouseMotion { Position = point, GlobalPosition = point, ButtonMask = MouseButtonMask.Left }, true);
            await Frames(1);
            TestScene.GetViewport().PushInput(new InputEventMouseButton
            {
                ButtonIndex = MouseButton.Left,
                Pressed = false,
                Position = point,
                GlobalPosition = point
            }, true);
            await Frames(2);
            TestScene.GetViewport().GuiIsDragSuccessful().ShouldBeTrue();
            state.ReadEquipment(owner).OwnedItems[item.Id].ShouldBeSameAs(item);
            editor.GetNode<Control>("AffixScroll/AffixRows/Affix0").GetNode<SpinBox>("Quality").Value.ShouldBe(73);
            new EquipmentService(state).EquipItem(owner, item.Id, EquipmentSlot.Weapon).ShouldBe(EquipmentChangeStatus.Applied);
            var row = editor.GetNode<Control>("AffixScroll/AffixRows/Affix1");
            Set(row.GetNode<OptionButton>("Family"), Find(row.GetNode<OptionButton>("Family"), ItemPresentation.AffixName("ignite-on-hit")));
            editor.GetNode<Button>("Apply").EmitSignal(Button.SignalName.Pressed);
            await Frames(2);
            var edited = state.ReadEquipment(owner).OwnedItems[item.Id];
            edited.Id.ShouldBe(item.Id);
            edited.Affixes.Count.ShouldBe(2);
            edited.Affixes.Single(value => value.DefinitionId == "poison-on-hit").ShouldBeSameAs(item.Affixes[0]);
            state.ReadEquipment(owner).Loadout.GetItem(EquipmentSlot.Weapon).ShouldBeSameAs(edited);
            state.GetStats(owner).Get(new(StatType.IgniteChance)).ShouldBe(0.5m);
            state.GetStats(owner).Get(new(StatType.PoisonChance)).ShouldBe(0.1m);
            await Capture("admin-editor.png");
            admin.Hide();
        }
        finally
        {
            arena.Free();
        }
    }

    /// <summary>Eight real Ukrainian affixes fit a compact card; only Alt reveals current tiers and no available pools appear.</summary>
    /// <returns>Completes after native text measurement and optional actual rendered captures.</returns>
    [Test]
    public async Task LegendaryUkrainianCardIsCompactAndAltShowsOnlyCurrentTiers()
    {
        var previous = TranslationServer.GetLocale();
        TranslationServer.SetLocale("uk");
        var host = new Control { Size = TestScene.GetViewport().GetVisibleRect().Size };
        TestScene.AddChild(host);
        var design = UiLayout.Design(host, true);
        var card = new ItemDetailsPanel { Position = new(16, 16) };
        design.AddChild(card);

        try
        {
            var source = AdminItemCatalog.Empty("dexterity-weapon-travel", Guid.NewGuid().ToString(), ItemRarity.Legendary, 70);
            var names = new[] { "bleed-on-hit", "poison-on-hit", "ignite-on-hit", "global-damage", "resist-all", "all-attributes", "added-physical", "increased-physical" };
            var available = AdminItemCatalog.Families(source.Base, 70);
            names = names
                .Where(id => available.Any(value => value.GetProperty("id").GetString() == id))
                .Concat(available
                    .Select(value => value.GetProperty("id").GetString())
                    .Where(id => !names.Contains(id)))
                .Take(8)
                .ToArray();
            var item = AdminItemCatalog.Build(source, ItemRarity.Legendary, 70, names
                .Select(id => new AdminAffixChoice(id, "T1", 100))
                .ToArray());
            card.ShowItem(item, EquipmentSlot.Weapon, true, null);
            await Frames(3);
            var properties = card.GetNode<RichTextLabel>("Card/Properties");
            var plain = properties.GetParsedText();
            plain.ShouldNotContain("T1");
            plain.ShouldNotContain("за тик");
            plain.ShouldContain("шанс кровотечі");
            plain.ShouldNotContain(ItemPresentation.AffixName("bleed-on-hit"));
            ((float)properties.GetContentHeight()).ShouldBeLessThanOrEqualTo(properties.Size.Y + 4);
            card.GetRect().End.Y.ShouldBeLessThanOrEqualTo(design.Size.Y);
            await Capture("legendary-uk.png");
            card.SetDetailed(true);
            await Frames(2);
            properties.GetParsedText().ShouldContain("T1");
            properties.GetParsedText().ShouldNotContain("Доступні тіри");
            properties.GetParsedText().ShouldNotContain("T1–T7");
            await Capture("legendary-uk-alt.png");
        }
        finally
        {
            host.Free();
            TranslationServer.SetLocale(previous);
        }
    }

    private GreyboxArena Create()
    {
        var arena = GD.Load<PackedScene>("res://Arena.tscn").Instantiate<GreyboxArena>();
        arena.RoundsEnabled = true;
        Runtime.AddChild(arena);
        arena.SelectStarterWeapon("dexterity-weapon-travel").ShouldBeTrue();
        arena.RoundsEnabled = false;
        arena.Player.Controls.HardwareInput = false;
        arena.Opponent.Controls.ThinkingEnabled = false;
        return arena;
    }

    private static int Find(OptionButton picker, string text) => Enumerable.Range(0, picker.ItemCount)
        .Single(index => picker.GetItemText(index) == text);

    private static void Set(OptionButton picker, int index)
    {
        picker.Select(index);
        picker.EmitSignal(OptionButton.SignalName.ItemSelected, index);
    }

    private async Task Frames(int count)
    {
        for (var frame = 0; frame < count; frame++)
        {
            await TestScene.ToSignal(TestScene.GetTree(), SceneTree.SignalName.PhysicsFrame);
        }
    }

    private async Task Capture(string name)
    {
        var folder = OS.GetEnvironment("RFH_ADMIN_CARD_CAPTURE_DIRECTORY");

        if (!string.IsNullOrEmpty(folder))
        {
            await TestScene.ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
            TestScene.GetViewport().GetTexture().GetImage().SavePng(Path.Combine(folder, name)).ShouldBe(Error.Ok);
        }
    }
}
