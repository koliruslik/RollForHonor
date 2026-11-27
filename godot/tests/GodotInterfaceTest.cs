using System;
using System.Linq;
using System.Threading.Tasks;
using Chickensoft.GoDotTest;
using Godot;
using RollForHonor.Domain.Items.Configuration;
using RollForHonor.Domain.Items.Equipment.Models;
using RollForHonor.Domain.Items.Models;
using RollForHonor.Domain.Items.Affixes.Models;
using RollForHonor.Domain.Items.Affixes.Services;
using RollForHonor.Domain.Items.Services;
using RollForHonor.Domain.Stats;
using RollForHonor.Godot.Arena;
using RollForHonor.Godot.Content.Items;
using RollForHonor.Godot.Gameplay.Movement;
using RollForHonor.Godot.UI.Common;
using RollForHonor.Godot.UI.Hud;
using RollForHonor.Godot.UI.Inventory;
using RollForHonor.Godot.Gameplay.Targeting;
using RollForHonor.Godot.Gameplay.Input;
using RollForHonor.Godot.Gameplay.Combat;
using RollForHonor.Godot.UI.Menu;
using RollForHonor.Godot.UI.Settings;
using Shouldly;
using RollForHonor.Infrastructure.Dice;

namespace RollForHonor.Godot.Tests;

/// <summary>Verifies real native interface interactions, preference persistence and live equipment operations.</summary>
public sealed class GodotInterfaceTest : TestClass
{
    private GameBootstrap Runtime => TestScene.GetTree().Root.GetChildren()
        .OfType<GameBootstrap>()
        .Single();

    /// <summary>Creates native interface checks.</summary>
    /// <param name="testScene">The engine test host.</param>
    public GodotInterfaceTest(Node testScene) : base(testScene)
    {
    }

    /// <summary>Native title buttons expose the requested game, settings, reference and exit navigation intents.</summary>
    [Test]
    public void TitleButtonsEmitTheirNavigationIntents()
    {
        var view = new MainMenuView();
        TestScene.AddChild(view);

        try
        {
            var started = 0;
            var settings = 0;
            var exited = 0;
            var references = 0;
            view.StartRequested += () => started++;
            view.SettingsRequested += () => settings++;
            view.ExitRequested += () => exited++;
            view.ReferenceRequested += () => references++;

            view.GetNode<Button>("Design/Start").EmitSignal(BaseButton.SignalName.Pressed);
            view.GetNode<Button>("Design/Settings").EmitSignal(BaseButton.SignalName.Pressed);
            view.GetNode<Button>("Design/Exit").EmitSignal(BaseButton.SignalName.Pressed);
            view.GetNode<Button>("Design/Reference").EmitSignal(BaseButton.SignalName.Pressed);

            started.ShouldBe(1);
            settings.ShouldBe(1);
            exited.ShouldBe(1);
            references.ShouldBe(1);
        }
        finally
        {
            view.Free();
        }
    }

    /// <summary>Closing a settings form discards unapplied edits and reopening restores the saved values.</summary>
    [Test]
    public void SettingsCloseDiscardsUnappliedValues()
    {
        var settings = new SettingsPanel();
        TestScene.AddChild(settings);

        try
        {
            settings.Open();
            var slider = settings.GetNode<HSlider>("Design/Settings/Volume");
            var initial = slider.Value;
            slider.Value = initial > 20 ? 10 : 90;

            settings.Close();
            settings.Visible.ShouldBeFalse();
            settings.Open();

            slider.Value.ShouldBe(initial);
        }
        finally
        {
            settings.Free();
        }
    }

    /// <summary>Preferences round-trip through a separate file without modifying real user settings or the display.</summary>
    [Test]
    public void PreferencesRoundTripAndValidateInvalidFieldTypes()
    {
        var path = $"user://ui-test-{Guid.NewGuid():N}.cfg";
        var store = new PreferencesStore(path);

        try
        {
            var requested = new PlayerPreferences(true, false, 0.43f, false, true, true);
            store.Save(requested).ShouldBe(Error.Ok);
            store.Load().ShouldBe(requested);
            using var config = new ConfigFile();
            config.SetValue("presentation", "fullscreen", "invalid");
            config.SetValue("presentation", "volume", 20.0);
            config.Save(path).ShouldBe(Error.Ok);

            var validated = store.Load();
            validated.Fullscreen.ShouldBeFalse();
            validated.MasterVolume.ShouldBe(1);
        }
        finally
        {
            DirAccess.RemoveAbsolute(ProjectSettings.GlobalizePath(path));
        }
    }

    /// <summary>Inventory and character panels remain independent while equipment operations and world processing continue.</summary>
    /// <returns>Completes after live physics consumes the final published stats.</returns>
    [Test]
    public async Task InventoryButtonsChangeRealEquipmentAndRestoreWorldProcessing()
    {
        var arena = CreateArena();

        try
        {
            var hud = arena.GetNode<ArenaHud>("Presentation");
            var id = arena.Player.CombatantId;
            Runtime.CombatState.ReadEquipment(id).OwnedItems.Count.ShouldBe(12);
            hud.ToggleInventory();
            TestScene.GetTree().Paused.ShouldBeFalse();
            ((CharacterPanel)hud.FindChild("Character", true, false)).Visible.ShouldBeFalse();
            hud.ToggleCharacter();
            var speedValue = hud.FindChild("Character", true, false).FindChildren("*", "Label", true, false)
                .Cast<Label>()
                .Single(label => label.Text == "Movement speed")
                .GetParent()
                .GetChild<Label>(1);
            speedValue.Text.ShouldBe("0%");
            var before = Runtime.GameTime;
            var position = arena.Player.Position;
            arena.Player.Controls.MovementIntent = Vector2.Right;
            await Frames(6);
            arena.Player.Position.DistanceTo(position).ShouldBeGreaterThan(0.01f);
            arena.Player.Controls.MovementIntent = Vector2.Zero;
            Runtime._Process(0.05);
            Runtime.GameTime.ShouldBeGreaterThan(before);
            hud.FindChild("wayfarer-boots", true, false).EmitSignal(BaseButton.SignalName.Pressed);
            var action = hud.FindChild("ItemDetails", true, false).GetNode<Button>("Card/Action");

            action.EmitSignal(BaseButton.SignalName.Pressed);

            Runtime.CombatState.ReadEquipment(id).Loadout.GetItem(EquipmentSlot.Boots).ShouldNotBeNull();
            Runtime.CombatState.GetStats(id).Get(new StatKey(StatType.MovementSpeed)).ShouldBe(6.84m);
            hud._Process(0);
            speedValue.Text.ShouldBe("+14%");
            action.Text.ShouldStartWith("Unequip");
            Runtime.GameTime.ShouldBeGreaterThan(before);
            action.EmitSignal(BaseButton.SignalName.Pressed);
            Runtime.CombatState.ReadEquipment(id).Loadout.GetItem(EquipmentSlot.Boots).ShouldBeNull();
            hud._Process(0);
            speedValue.Text.ShouldBe("0%");
            hud.ToggleInventory();
            ((CharacterPanel)hud.FindChild("Character", true, false)).Visible.ShouldBeTrue();
            TestScene.GetTree().Paused.ShouldBeFalse();
            await Frames(3);

            arena.Player.GetRequiredComponent<MovementMotor>().Speed.ShouldBe(6);
        }
        finally
        {
            arena.Free();
        }
    }

    /// <summary>Closing or freeing an overlay cannot resume a world which was already paused by another owner.</summary>
    [Test]
    public void InterfaceRestoresThePreviousPauseState()
    {
        var arena = CreateArena();
        var tree = TestScene.GetTree();
        var original = tree.Paused;

        try
        {
            tree.Paused = true;
            var hud = arena.GetNode<ArenaHud>("Presentation");
            hud.ToggleInventory();
            hud.ToggleInventory();
            tree.Paused.ShouldBeTrue();
            hud.ToggleInventory();
            arena.Free();
            tree.Paused.ShouldBeTrue();
        }
        finally
        {
            if (GodotObject.IsInstanceValid(arena))
            {
                arena.Free();
            }

            tree.Paused = original;
        }
    }

    /// <summary>Valid owned items without equipment tags remain inspectable and cannot invoke an equip action.</summary>
    [Test]
    public void NonEquipmentItemsDoNotBreakTheInventory()
    {
        var arena = CreateArena();

        try
        {
            var item = new ItemFactory(ItemRarityRules.Default, ItemStatRules.Default)
                .Create($"material-{Guid.NewGuid():N}", new ItemDefinition("material", "Uncut stone"), ItemRarity.Common);
            Runtime.CombatState.RegisterOwnedItem(arena.Player.CombatantId, item);
            var hud = arena.GetNode<ArenaHud>("Presentation");
            hud.ToggleInventory();

            hud.FindChild("material", true, false).EmitSignal(BaseButton.SignalName.Pressed);

            hud.FindChild("ItemDetails", true, false).GetNode<Button>("Card/Action").Disabled.ShouldBeTrue();
            Runtime.CombatState.ReadEquipment(arena.Player.CombatantId).Loadout.Slots.ShouldBeEmpty();
        }
        finally
        {
            arena.Free();
        }
    }

    /// <summary>Native right clicks equip and unequip; replacing a slot offers dismissible, accurate item comparisons.</summary>
    [Test]
    public void RightClickAndComparisonUseOwnedItems()
    {
        var arena = CreateArena();

        try
        {
            var hud = arena.GetNode<ArenaHud>("Presentation");
            hud.ToggleInventory();
            var coat = (ItemCell)hud.FindChild("embercoat", true, false);
            using var rightClick = new InputEventMouseButton { ButtonIndex = MouseButton.Right, Pressed = true };
            coat._GuiInput(rightClick);
            ((ItemDetailsPanel)hud.FindChild("ItemDetails", true, false)).Visible.ShouldBeFalse();
            var read = Runtime.CombatState.ReadEquipment(arena.Player.CombatantId);
            read.Loadout.GetItem(EquipmentSlot.BodyArmor).Base.Id.ShouldBe("embercoat");
            var other = (ItemCell)hud.FindChild("pilgrim-coat", true, false);
            other.EmitSignal(BaseButton.SignalName.Pressed);
            var comparison = (ItemDetailsPanel)hud.FindChild("Comparison", true, false);
            comparison.Visible.ShouldBeTrue();
            comparison.GetNode<RichTextLabel>("Card/Properties").GetParsedText().ShouldContain("+3 armor");
            comparison.GetNode<Button>("Card/Header/Close").EmitSignal(BaseButton.SignalName.Pressed);
            comparison.Visible.ShouldBeFalse();
            var details = (ItemDetailsPanel)hud.FindChild("ItemDetails", true, false);
            var action = details.GetNode<Button>("Card/Action");
            var status = details.GetNode<Label>("Card/Status");
            status.Position.Y.ShouldBeGreaterThanOrEqualTo(action.GetRect().End.Y + 16);
            details.GetNode<Panel>("Card").Size.Y.ShouldBeGreaterThanOrEqualTo(status.GetRect().End.Y + 20);
            details.GetNode<Button>("Card/Header/Close").EmitSignal(BaseButton.SignalName.Pressed);
            hud._Process(0);
            details.Visible.ShouldBeFalse();
            ((InventoryPanel)hud.FindChild("Inventory", true, false)).Visible.ShouldBeTrue();
            ((ItemCell)hud.FindChild("BodyArmor", true, false))._GuiInput(rightClick);
            Runtime.CombatState.ReadEquipment(arena.Player.CombatantId).Loadout.GetItem(EquipmentSlot.BodyArmor).ShouldBeNull();
        }
        finally
        {
            arena.Free();
        }
    }

    /// <summary>Native drop callbacks reject incompatible or foreign identities and return equipped items to the grid.</summary>
    [Test]
    public void DragDropValidatesOwnershipAndSlotCompatibility()
    {
        var arena = CreateArena();

        try
        {
            var hud = arena.GetNode<ArenaHud>("Presentation");
            hud.ToggleInventory();
            var boots = (ItemCell)hud.FindChild("wayfarer-boots", true, false);
            var slot = (ItemCell)hud.FindChild("Boots", true, false);
            var payload = new global::Godot.Collections.Dictionary { ["rfh-item-id"] = boots.ItemId };
            var grid = (InventoryGridView)hud.FindChild("Grid", true, false);
            var previous = grid.Layout.Placements[boots.ItemId].Position;
            slot._CanDropData(Vector2.Zero, payload).ShouldBeTrue();
            var weapon = (ItemCell)hud.FindChild("Weapon", true, false);
            weapon._CanDropData(Vector2.Zero, payload).ShouldBeFalse();
            slot._CanDropData(Vector2.Zero, new global::Godot.Collections.Dictionary { ["rfh-item-id"] = "foreign" }).ShouldBeFalse();
            slot._CanDropData(Vector2.Zero, 42).ShouldBeFalse();
            slot._DropData(Vector2.Zero, payload);
            Runtime.CombatState.ReadEquipment(arena.Player.CombatantId).Loadout.GetItem(EquipmentSlot.Boots).ShouldNotBeNull();
            var destination = new Vector2(previous.Column, previous.Row) * InventoryGridView.CellSize + new Vector2(4, 4);
            grid._CanDropData(destination, payload).ShouldBeTrue();
            grid._DropData(destination, payload);
            var read = Runtime.CombatState.ReadEquipment(arena.Player.CombatantId);
            read.Loadout.GetItem(EquipmentSlot.Boots).ShouldBeNull();
            read.OwnedItems.Count.ShouldBe(12);
        }
        finally
        {
            arena.Free();
        }
    }

    /// <summary>Opening side panels preserves HUD positions and the top target bar follows explicit selection.</summary>
    [Test]
    public void SidePanelsDoNotMoveHudAndTargetBarRequiresSelection()
    {
        var arena = CreateArena();

        try
        {
            var hud = arena.GetNode<ArenaHud>("Presentation");
            var design = hud.GetNode<Control>("Root/Design");
            var position = design.GetNode<Panel>("ActionBar").Position;
            design.GetNode<Panel>("Enemy").Visible.ShouldBeFalse();
            hud.ToggleInventory();
            hud.ToggleCharacter();
            design.GetNode<Panel>("ActionBar").Position.ShouldBe(position);
            var selection = arena.Player.GetRequiredComponent<CombatAwareness>().Selection;
            selection.Select(arena.Opponent.CombatantId);
            hud._Process(0);
            design.GetNode<Panel>("Enemy").Visible.ShouldBeTrue();
            selection.Select(null);
            hud._Process(0);
            design.GetNode<Panel>("Enemy").Visible.ShouldBeFalse();
        }
        finally
        {
            arena.Free();
        }
    }

    /// <summary>Hover always shows individual affixes; Alt adds retained intervals, quality and the actual tier pool.</summary>
    [Test]
    public void HoverAndAltDetailsUseActualAffixRanges()
    {
        var arena = CreateArena();

        try
        {
            var hud = arena.GetNode<ArenaHud>("Presentation");
            hud.ToggleInventory();
            hud.FindChild("ashen-longbow", true, false).EmitSignal(Control.SignalName.MouseEntered);
            var tooltip = (ItemDetailsPanel)hud.FindChild("ItemTooltip", true, false);
            tooltip.Visible.ShouldBeTrue();
            var properties = tooltip.GetNode<RichTextLabel>("Card/Properties");
            properties.GetParsedText().ShouldContain("6–11");
            properties.GetParsedText().ShouldNotContain("1d6 + 5");
            properties.GetParsedText().ShouldContain("AFFIXES");
            properties.GetParsedText().ShouldNotContain("steady hand");
            properties.GetParsedText().ShouldNotContain("T1");
            properties.GetParsedText().ShouldContain("+2 accuracy");
            properties.GetParsedText().ShouldNotContain("barbed shaft");
            properties.GetParsedText().ShouldNotContain("72%");
            properties.GetParsedText().ShouldNotContain("Available tiers:");
            tooltip.SetDetailed(true);
            properties.GetParsedText().ShouldContain("1d6 + 5");
            properties.GetParsedText().ShouldContain("[1/3] 72% T1");
            properties.GetParsedText().ShouldNotContain("Available tiers:");
            properties.GetParsedText().ShouldNotContain("T1–T16");
            tooltip.SetDetailed(false);
            properties.GetParsedText().ShouldNotContain("72%");
            properties.GetParsedText().ShouldNotContain("steady hand");
            properties.GetParsedText().ShouldNotContain("T1");
        }
        finally
        {
            arena.Free();
        }
    }

    /// <summary>Alt shows only the selected tier, independent of how many tiers an affix defines.</summary>
    [Test]
    public void AltShowsOnlyCurrentAffixTier()
    {
        var card = new ItemDetailsPanel();
        TestScene.AddChild(card);

        try
        {
            foreach (var count in new[] { 16, 15, 4 })
            {
                var tiers = Enumerable.Range(1, count)
                    .Reverse()
                    .Select(rank => new AffixTier($"T{rank}", count - rank,
                        [new AffixModifierRange(new StatKey(StatType.MaxHealth), StatOperation.Flat, count - rank + 1, count - rank + 2)]));
                var definition = new AffixDefinition("vitality", "health", 1, tiers);
                var affix = new AffixRoller().Roll("rolled", definition, $"T{count}", new RandomDiceRoller());
                var item = new ItemFactory(ItemRarityRules.Default, ItemStatRules.Default)
                    .Create("owned", new ItemDefinition("coat", "Coat"), ItemRarity.Uncommon, [affix]);
                card.SetDetailed(true);
                card.ShowItem(item, null, false, null);
                var text = card.GetNode<RichTextLabel>("Card/Properties").GetParsedText();
                text.ShouldContain($"T{count}");
                text.ShouldNotContain("Available tiers:");
                card.SetDetailed(false);
                text = card.GetNode<RichTextLabel>("Card/Properties").GetParsedText();
                text.ShouldNotContain($"T{count}");
                text.ShouldContain("health");
                text.ShouldNotContain("Available tiers:");
            }
        }
        finally
        {
            card.Free();
        }
    }

    /// <summary>Pinned cards grow with affixes and Alt details, shrink again and omit unused comparison action space.</summary>
    /// <returns>Completes after isolated native card geometry stabilizes between content changes.</returns>
    [Test]
    public async Task ItemCardHeightFollowsItsVisibleContent()
    {
        var host = new Control { Size = new Vector2(1920, 1080), Theme = HonorTheme.Create() };
        TestScene.AddChild(host);
        var card = new ItemDetailsPanel();
        host.AddChild(card);

        try
        {
            var owned = StarterItemCatalog.Create(new(Guid.NewGuid()));
            var simple = owned.Single(item => item.Base.Id == "pilgrim-coat");
            var complex = owned.Single(item => item.Base.Id == "ashen-longbow");
            card.ShowItem(simple, EquipmentSlot.BodyArmor, false, null);
            await WaitForCardLayout(card);
            var simpleHeight = card.Size.Y;
            card.SetStatus("Equipped. Stats updated.");
            card.ShowItem(complex, EquipmentSlot.Weapon, false, null);
            card.GetNode<Label>("Card/Status").Text.ShouldNotContain("Equipped");
            await WaitForCardLayout(card);
            var ordinaryHeight = card.Size.Y;
            ordinaryHeight.ShouldBeGreaterThan(simpleHeight);

            for (var cycle = 0; cycle < 2; cycle++)
            {
                card.SetDetailed(true);
                await WaitForCardLayout(card);
                card.Size.Y.ShouldBeGreaterThanOrEqualTo(ordinaryHeight);
                card.SetDetailed(false);
                await WaitForCardLayout(card);
                card.Size.Y.ShouldBe(ordinaryHeight);
            }

            card.ShowComparison(complex, EquipmentSlot.Weapon);
            await WaitForCardLayout(card);
            card.Size.Y.ShouldBeLessThan(ordinaryHeight);
            card.GetNode<RichTextLabel>("Card/Properties").Size.Y.ShouldBeGreaterThanOrEqualTo(
                card.GetNode<RichTextLabel>("Card/Properties").GetContentHeight());
        }
        finally
        {
            host.Free();
        }
    }

    /// <summary>Oversized descriptions scroll while the card, action and result remain inside its available screen area.</summary>
    /// <returns>Completes after native text wrapping and scrollbar layout settle.</returns>
    [Test]
    public async Task LongItemCardsKeepTheirActionsInsideTheViewport()
    {
        var host = new Control { Size = new Vector2(1920, 1080) };
        TestScene.AddChild(host);
        var card = new ItemDetailsPanel { Position = new Vector2(16, 16) };
        host.AddChild(card);

        try
        {
            var ranges = Enumerable.Range(0, 50)
                .Select(_ => new AffixModifierRange(new StatKey(StatType.MaxHealth), StatOperation.Flat, 10, 20));
            var definition = new AffixDefinition("long-description", "health", 1, [new AffixTier("T1", 0, ranges)]);
            var affix = new AffixRoller().Roll("rolled", definition, "T1", new RandomDiceRoller());
            var item = new ItemFactory(ItemRarityRules.Default, ItemStatRules.Default)
                .Create("owned", new ItemDefinition("coat", "Coat"), ItemRarity.Uncommon, [affix]);
            card.ShowItem(item, EquipmentSlot.BodyArmor, false, null);
            card.SetDetailed(true);
            card.SetStatus("Equipped. Character statistics and equipment properties have been updated.");
            await Frames(2);
            var properties = card.GetNode<RichTextLabel>("Card/Properties");
            ((float)properties.GetContentHeight()).ShouldBeGreaterThan(properties.Size.Y);
            properties.ScrollActive.ShouldBeTrue();
            (card.Position.Y + card.Size.Y).ShouldBeLessThanOrEqualTo(host.Size.Y - 16);
            var action = card.GetNode<Button>("Card/Action");
            var status = card.GetNode<Label>("Card/Status");
            status.Position.Y.ShouldBeGreaterThanOrEqualTo(action.GetRect().End.Y + 16);
            status.Size.Y.ShouldBeGreaterThanOrEqualTo(status.GetCombinedMinimumSize().Y);
            card.Size.Y.ShouldBeGreaterThanOrEqualTo(status.GetRect().End.Y + 20);
        }
        finally
        {
            host.Free();
        }
    }

    /// <summary>Equipped weapon mana cost, dice and cooldown are the same properties displayed by the card.</summary>
    [Test]
    public void EquippedWeaponControlsActualLaunchContent()
    {
        var arena = CreateArena();

        try
        {
            var hud = arena.GetNode<ArenaHud>("Presentation");
            hud.ToggleInventory();
            var bow = (ItemCell)hud.FindChild("iron-bow", true, false);
            using var rightClick = new InputEventMouseButton { ButtonIndex = MouseButton.Right, Pressed = true };
            bow._GuiInput(rightClick);
            var before = Runtime.CombatState.ReadForStart(arena.Player.CombatantId, "arena-shot", Guid.NewGuid());
            var launch = arena.Player.GetRequiredComponent<ProjectileAttack>().RequestAttack(arena.Player.GlobalPosition + new Vector3(0, 1, -4));
            launch.Attack.ShouldNotBeNull();
            launch.Attack.Attack.Damage.Single().Dice.Sides.ShouldBe(4);
            var after = Runtime.CombatState.ReadForStart(arena.Player.CombatantId, "arena-shot", Guid.NewGuid());
            after.Resources.Single().Amount.ShouldBe(before.Resources.Single().Amount - 6);
            after.ReadyAt.ShouldBe(Runtime.GameTime + TimeSpan.FromSeconds(1d / 3));
        }
        finally
        {
            arena.Free();
        }
    }

    /// <summary>Changing the action map updates both the interface hotkey and its hint without moving HUD elements.</summary>
    [Test]
    public void InterfaceHintsAndInputFollowTheSameActionMap()
    {
        var arena = CreateArena();
        var original = InputMap.ActionGetEvents("rfh_inventory");

        try
        {
            InputMap.ActionEraseEvents("rfh_inventory");
            InputMap.ActionAddEvent("rfh_inventory", new InputEventKey { PhysicalKeycode = Key.K });
            var hud = arena.GetNode<ArenaHud>("Presentation");
            hud._Process(0);
            hud.GetNode<Panel>("Root/Design/Hints").GetChild<Label>(0).Text.ShouldContain("K  Inventory");
            using var key = new InputEventKey { PhysicalKeycode = Key.K, Pressed = true };
            hud._Input(key);
            hud._Process(0);
            ((InventoryPanel)hud.FindChild("Inventory", true, false)).Visible.ShouldBeTrue();
            GameInputBindings.Name("rfh_inventory").ShouldBe("K");
            hud.GetNode<Panel>("Root/Design/Hints").Visible.ShouldBeFalse();
            ((Label)hud.FindChild("InventoryHints", true, false)).Text.ShouldContain("K  Close inventory");
        }
        finally
        {
            InputMap.ActionEraseEvents("rfh_inventory");

            foreach (var item in original)
            {
                InputMap.ActionAddEvent("rfh_inventory", item);
            }

            arena.Free();
        }
    }

    /// <summary>Each open panel owns its compact hints; gameplay hints return after panels close and stay hidden during pause.</summary>
    [Test]
    public void HintsFollowTheActiveInterfaceContext()
    {
        var arena = CreateArena();

        try
        {
            var hud = arena.GetNode<ArenaHud>("Presentation");
            var gameplay = hud.GetNode<Panel>("Root/Design/Hints");
            var gameplayText = gameplay.GetChild<Label>(0);
            gameplay.Visible.ShouldBeTrue();
            gameplayText.Text.ShouldContain("Attack");
            gameplayText.Text.ShouldNotContain("Compare");
            gameplayText.Text.ShouldNotContain("Affix");
            gameplayText.GetThemeFontSize("font_size").ShouldBe(12);
            gameplayText.GetThemeColor("font_color").A.ShouldBeLessThan(0.6f);

            hud.ToggleInventory();
            gameplay.Visible.ShouldBeFalse();
            var inventory = (Label)hud.FindChild("InventoryHints", true, false);
            inventory.IsVisibleInTree().ShouldBeTrue();
            inventory.Text.ShouldContain("Compare");
            inventory.Text.ShouldContain("Equip / remove");
            inventory.Text.ShouldNotContain("Attack");
            inventory.GetThemeFontSize("font_size").ShouldBe(12);
            inventory.GetThemeColor("font_color").A.ShouldBeLessThan(0.6f);

            hud.ToggleCharacter();
            ((Label)hud.FindChild("CharacterHints", true, false)).IsVisibleInTree().ShouldBeTrue();
            inventory.IsVisibleInTree().ShouldBeTrue();
            hud.ToggleInventory();
            gameplay.Visible.ShouldBeFalse();
            inventory.IsVisibleInTree().ShouldBeFalse();
            hud.ToggleCharacter();
            gameplay.Visible.ShouldBeTrue();
            using var escape = new InputEventKey { PhysicalKeycode = Key.Escape, Pressed = true };
            hud._Input(escape);
            gameplay.Visible.ShouldBeFalse();
            hud._Input(escape);
            gameplay.Visible.ShouldBeTrue();
        }
        finally
        {
            arena.Free();
        }
    }

    /// <summary>Close controls retain equal width and height under the actual shared and settings themes.</summary>
    /// <returns>Completes after the native controls apply their theme minimum sizes.</returns>
    [Test]
    public async Task WindowCloseButtonsStaySquareUnderTheirThemes()
    {
        var arena = CreateArena();

        try
        {
            var hud = arena.GetNode<ArenaHud>("Presentation");
            hud.ToggleInventory();
            hud.ToggleCharacter();
            using var rightClick = new InputEventMouseButton { ButtonIndex = MouseButton.Right, Pressed = true };
            ((ItemCell)hud.FindChild("ashen-longbow", true, false))._GuiInput(rightClick);
            hud.FindChild("iron-bow", true, false).EmitSignal(BaseButton.SignalName.Pressed);
            hud.GetNode<SettingsPanel>("Root/Design/Settings").Open();
            await Frames(2);
            var buttons = hud.FindChildren("Close", "Button", true, false)
                .Cast<Button>()
                .Where(button => button.IsVisibleInTree())
                .ToArray();
            buttons.Length.ShouldBe(5);

            foreach (var button in buttons)
            {
                button.Size.ShouldBe(new Vector2(40, 40));
                button.GetGlobalRect().Size.X.ShouldBe(button.GetGlobalRect().Size.Y);
            }
        }
        finally
        {
            arena.Free();
        }
    }

    /// <summary>Metadata hover on a modified weapon property shows the captured contributing affix and interval.</summary>
    [Test]
    public void ModifiedPropertyHoverShowsItsCapturedAffix()
    {
        var arena = CreateArena();

        try
        {
            var hud = arena.GetNode<ArenaHud>("Presentation");
            hud.ToggleInventory();
            hud.FindChild("ashen-longbow", true, false).EmitSignal(BaseButton.SignalName.Pressed);
            var card = (ItemDetailsPanel)hud.FindChild("ItemDetails", true, false);
            var properties = card.GetNode<RichTextLabel>("Card/Properties");
            properties.EmitSignal(RichTextLabel.SignalName.MetaHoverStarted, "0");
            var tooltip = (FloatingTip)hud.FindChild("TextTooltip", true, false);
            tooltip.Visible.ShouldBeTrue();
            tooltip.GetChild<Label>(0).Text.ShouldContain("barbed shaft");
            tooltip.GetChild<Label>(0).Text.ShouldNotContain("72%");
            card.SetDetailed(true);
            properties.EmitSignal(RichTextLabel.SignalName.MetaHoverStarted, "0");
            tooltip.GetChild<Label>(0).Text.ShouldContain("[1/3] 72% T1");
            properties.EmitSignal(RichTextLabel.SignalName.MetaHoverEnded, "0");
            tooltip.Visible.ShouldBeFalse();
        }
        finally
        {
            arena.Free();
        }
    }

    private GreyboxArena CreateArena()
    {
        var arena = GD.Load<PackedScene>("res://Arena.tscn").Instantiate<GreyboxArena>();
        Runtime.AddChild(arena);
        arena.Player.Controls.HardwareInput = false;
        arena.Opponent.Controls.ThinkingEnabled = false;

        ArenaItemFixtures.UseAuthoredKit(arena);

        return arena;
    }

    private async Task Frames(int count)
    {
        for (var index = 0; index < count; index++)
        {
            await TestScene.ToSignal(TestScene.GetTree(), SceneTree.SignalName.PhysicsFrame);
        }
    }

    private async Task WaitForCardLayout(ItemDetailsPanel card)
    {
        var properties = card.GetNode<RichTextLabel>("Card/Properties");
        var previous = (card.Size, properties.Size, properties.GetContentHeight(), properties.GetVScrollBar().Visible);
        var stableFrames = 0;

        for (var frame = 0; frame < 60; frame++)
        {
            await TestScene.ToSignal(TestScene.GetTree(), SceneTree.SignalName.ProcessFrame);
            var current = (card.Size, properties.Size, properties.GetContentHeight(), properties.GetVScrollBar().Visible);
            stableFrames = current == previous ? stableFrames + 1 : 0;

            if (stableFrames >= 3)
            {
                return;
            }

            previous = current;
        }

        throw new InvalidOperationException("Item card text and geometry did not stabilize within 60 process frames.");
    }
}
