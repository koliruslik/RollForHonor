using System.Linq;
using System.Threading.Tasks;
using Chickensoft.GoDotTest;
using Godot;
using RollForHonor.Domain.Stats;
using RollForHonor.Godot.Arena;
using RollForHonor.Godot.UI.Common;
using RollForHonor.Godot.UI.Hud;
using RollForHonor.Godot.UI.Inventory;
using RollForHonor.Godot.UI.Menu;
using RollForHonor.Godot.UI.Localization;
using RollForHonor.Godot.UI.Reference;
using Shouldly;

namespace RollForHonor.Godot.Tests;

/// <summary>Verifies shared explanations, reference filtering, retained pins and input/pause ownership.</summary>
public sealed class GodotReferenceTest : TestClass
{
    /// <summary>Creates reference checks against native Godot controls.</summary>
    /// <param name="testScene">The engine test host.</param>
    public GodotReferenceTest(Node testScene) : base(testScene)
    {
    }

    /// <summary>Catalog inspection reuses actual captured affixes without changing ownership or producing rolls.</summary>
    [Test]
    public void CatalogContainsSharedRulesAndActualAffixes()
    {
        var arena = CreateArena();

        try
        {
            var state = arena.GetParent<GameBootstrap>().CombatState;
            var before = state.ReadEquipment(arena.Player.CombatantId);
            var entries = ReferenceCatalog.Create(before.OwnedItems.Values);
            entries.Select(entry => entry.Id)
                .Distinct()
                .Count().ShouldBe(entries.Count);
            entries.Single(entry => entry.Id == "stat.Armor").Topic.Description.ShouldBe(CombatGlossary.Armor().Description);
            entries.Any(entry => entry.Id == "stat.CriticalChance").ShouldBeFalse();
            entries.Count(entry => entry.Category == L.Text("Owned items")).ShouldBe(before.OwnedItems.Count);
            entries.Any(entry => entry.Id.StartsWith("affix.", System.StringComparison.Ordinal)).ShouldBeFalse();
            entries.Single(entry => entry.Id == "loot.affixes").Table.Rows.Count.ShouldBe(58);
            entries.Single(entry => entry.Id == "loot.bases").Table.Rows.Count.ShouldBe(525);
            state.ReadEquipment(arena.Player.CombatantId).Version.ShouldBe(before.Version);

            foreach (var entry in entries)
            {
                foreach (var link in entry.Topic.Links)
                {
                    link.Open().Title.ShouldNotBeNullOrWhiteSpace();
                }
            }
        }
        finally
        {
            arena.Free();
        }
    }

    /// <summary>Search and category filters combine, clear stale articles and recover after no results.</summary>
    [Test]
    public void SearchAndCategoriesRetainOnlyMatchingEntries()
    {
        var panel = new ReferencePanel();
        TestScene.AddChild(panel);

        try
        {
            var entries = ReferenceCatalog.Create();
            panel.Open(entries);
            panel.SelectSection("rules");
            var search = panel.GetNode<LineEdit>("Book/Search");
            var category = panel.GetNode<OptionButton>("Book/Category");
            var list = (VBoxContainer)panel.FindChild("Entries", true, false);
            Search(panel, "FIRE resistance");
            list.GetChildCount().ShouldBe(entries.Count(entry => !entry.Id.StartsWith("loot.", System.StringComparison.Ordinal) && entry.Matches("FIRE resistance")));
            var index = Enumerable.Range(0, category.ItemCount).Single(value => category.GetItemText(value) == L.Text("Resistances"));
            category.Select(index);
            category.EmitSignal(OptionButton.SignalName.ItemSelected, index);
            list.GetChildCount().ShouldBe(1);
            ((Button)list.GetChild(0)).GetMeta("reference_id").AsString().ShouldBe("resistance.Fire");
            Search(panel, "nothing-matches-this-query");
            list.GetChildCount().ShouldBe(0);
            panel.GetNode<Button>("Book/Inspect").Disabled.ShouldBeTrue();
            panel.GetNode<RichTextLabel>("Book/Article").GetParsedText().ShouldContain(L.Text("Try another search in this section."));
            search.Text = "";
            panel.Open(entries);
            list.GetChildCount().ShouldBe(7);
        }
        finally
        {
            panel.Free();
        }
    }

    /// <summary>Bestiary navigation and slot-filtered tables replace a global article list without changing gameplay state.</summary>
    [Test]
    public void BestiaryAndTablesStayInTheirOwnSections()
    {
        var panel = new ReferencePanel();
        TestScene.AddChild(panel);

        try
        {
            panel.Open(ReferenceCatalog.Create());
            var list = (VBoxContainer)panel.FindChild("Entries", true, false);
            list.GetChildCount().ShouldBe(2);
            list.GetChildren().OfType<Button>()
                .All(button => button.GetMeta("reference_id").AsString().StartsWith("loot.monster.", System.StringComparison.Ordinal)).ShouldBeTrue();
            panel.GetNode<RichTextLabel>("Book/Article").GetParsedText().ShouldContain("40");
            panel.GetNode<ReferenceTableView>("Book/Table").VisibleRowCount.ShouldBe(106);
            panel.SelectSection("affixes");
            var category = panel.GetNode<OptionButton>("Book/Category");
            var helmet = Enumerable.Range(0, category.ItemCount).Single(index => category.GetItemText(index) == L.Text("Helmet"));
            category.Select(helmet);
            category.EmitSignal(OptionButton.SignalName.ItemSelected, helmet);
            Search(panel, "maximum health");
            panel.GetNode<ReferenceTableView>("Book/Table").VisibleRowCount.ShouldBe(1);
            panel.SelectSection("items");
            panel.GetNode<LineEdit>("Book/Search").Text.ShouldBe("");
            panel.GetNode<ReferenceTableView>("Book/Table").VisibleRowCount.ShouldBe(525);
            category.Selected.ShouldBe(0);
        }
        finally
        {
            panel.Free();
        }
    }

    /// <summary>Item cells open inline stat cards with nested explanations and tiers change retained ranges.</summary>
    /// <returns>Completes after an optional rendered item-card capture.</returns>
    [Test]
    public async Task ItemPropertiesAndAffixTiersAreInteractive()
    {
        var arena = CreateArena();

        try
        {
            var hud = arena.GetNode<ArenaHud>("Presentation");
            hud.ToggleReference();
            var panel = hud.GetNode<ReferencePanel>("Root/Design/Reference");
            panel.SelectSection("items");
            var data = LootReference.Create().Single(entry => entry.Id == "loot.bases").Table;
            data.Columns.Count.ShouldBe(4);
            data.Rows.Take(21).All(row => row.Id.EndsWith(".Common", System.StringComparison.Ordinal)).ShouldBeTrue();
            data.Rows.Skip(21).Take(21).All(row => row.Id.EndsWith(".Uncommon", System.StringComparison.Ordinal)).ShouldBeTrue();
            data.Rows.Skip(105).First().Id.ShouldEndWith(".Common");
            var table = panel.GetNode<ReferenceTableView>("Book/Table");
            var first = table.GetNode<VBoxContainer>("RowsScroll/Rows").GetChild<PanelContainer>(0);
            first.GetChild<HBoxContainer>(0).GetChild<Button>(3).EmitSignal(BaseButton.SignalName.Pressed);
            var stack = hud.GetNode<InspectionStack>("Root/Design/Inspections");
            stack.Count.ShouldBe(1);
            stack.GetNode<RichTextLabel>("Level0/Card/Body").EmitSignal(RichTextLabel.SignalName.MetaClicked, "1");
            stack.Count.ShouldBe(2);
            stack.GetNode<RichTextLabel>("Level1/Card/Body").EmitSignal(RichTextLabel.SignalName.MetaClicked, "0");
            stack.Count.ShouldBe(3);
            var armorRow = table.GetNode<VBoxContainer>("RowsScroll/Rows").GetChildren()
                .OfType<PanelContainer>()
                .Single(row => row.GetMeta("reference_row_id").AsString() == "strength-body-travel.Common");
            armorRow.GetChild<HBoxContainer>(0).GetChild<Button>(0).EmitSignal(BaseButton.SignalName.Pressed);
            var itemBody = stack.GetNode<RichTextLabel>("Level0/Card/Body");
            itemBody.GetParsedText().ShouldContain("65");
            itemBody.GetParsedText().ShouldContain("ilvl 1");
            itemBody.GetParsedText().ShouldNotContain(L.Text("RELATED"));
            var capture = OS.GetEnvironment("RFH_INLINE_CARD_CAPTURE");

            if (!string.IsNullOrWhiteSpace(capture))
            {
                await TestScene.ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
                TestScene.GetViewport().GetTexture().GetImage().SavePng(capture).ShouldBe(Error.Ok);
            }

            itemBody.EmitSignal(RichTextLabel.SignalName.MetaClicked, "2");
            stack.Count.ShouldBe(2);
            stack.GetNode<RichTextLabel>("Level1/Card/Body").GetParsedText().ShouldContain(CombatGlossary.Armor().Description);
            panel.SelectSection("affixes");
            Search(panel, "Maximum Health");
            var tier = panel.GetNode<OptionButton>("Book/Tier");
            tier.Visible.ShouldBeTrue();
            tier.ItemCount.ShouldBe(7);
            var range = table.GetNode<VBoxContainer>("RowsScroll/Rows").GetChild<PanelContainer>(0).GetChild<HBoxContainer>(0).GetChild<Button>(2);
            range.Text.ShouldBe("6–10");
            tier.Select(1);
            tier.EmitSignal(OptionButton.SignalName.ItemSelected, 1);
            table.VisibleRowCount.ShouldBe(1);
            range = table.GetNode<VBoxContainer>("RowsScroll/Rows").GetChild<PanelContainer>(0).GetChild<HBoxContainer>(0).GetChild<Button>(2);
            range.Text.ShouldBe("11–16");
        }
        finally
        {
            arena.Free();
        }
    }

    /// <summary>Reference cards retain the same links and can stay pinned after closing the reader and source windows.</summary>
    [Test]
    public void PinnedReferenceCardsSurviveClosingTheReader()
    {
        var arena = CreateArena();

        try
        {
            var hud = arena.GetNode<ArenaHud>("Presentation");
            hud.ToggleInventory();
            hud.ToggleReference();
            var panel = hud.GetNode<ReferencePanel>("Root/Design/Reference");
            TestScene.GetTree().Paused.ShouldBeTrue();
            Select(panel, "stat.Armor");
            panel.GetNode<Button>("Book/Inspect").EmitSignal(BaseButton.SignalName.Pressed);
            var stack = hud.GetNode<InspectionStack>("Root/Design/Inspections");
            stack.GetNode<RichTextLabel>("Level0/Card/Body").GetParsedText().ShouldContain(CombatGlossary.Armor().Description);
            stack.GetNode<RichTextLabel>("Level0/Card/Body").EmitSignal(RichTextLabel.SignalName.MetaClicked, "0");
            stack.Count.ShouldBe(2);
            stack.GetNode<Button>("Level1/Card/Pin").EmitSignal(BaseButton.SignalName.Pressed);
            panel.Close();
            TestScene.GetTree().Paused.ShouldBeFalse();
            stack.Count.ShouldBe(1);
            ((InspectionPane)stack.GetChild(0)).IsPinned.ShouldBeTrue();
            hud.FindChild("Inventory", true, false).GetParent<Control>().Visible.ShouldBeTrue();
        }
        finally
        {
            arena.Free();
        }
    }

    /// <summary>Typing in search cannot toggle inventory/character, and closing returns to a pre-existing pause.</summary>
    [Test]
    public void SearchDoesNotTriggerGameplayShortcutsOrLosePauseOwnership()
    {
        var arena = CreateArena();

        try
        {
            var hud = arena.GetNode<ArenaHud>("Presentation");
            using var escape = new InputEventKey { PhysicalKeycode = Key.Escape, Pressed = true };
            hud._Input(escape);
            TestScene.GetTree().Paused.ShouldBeTrue();
            hud.ToggleReference();
            var panel = hud.GetNode<ReferencePanel>("Root/Design/Reference");

            foreach (var key in new[] { Key.I, Key.C })
            {
                using var input = new InputEventKey { PhysicalKeycode = key, Pressed = true };
                hud._Input(input);
            }

            hud.GetNode<Control>("Root/Design/InventoryOverlay").Visible.ShouldBeFalse();
            panel.Visible.ShouldBeTrue();
            panel.Close();
            hud.GetNode<Panel>("Root/Design/Pause").Visible.ShouldBeTrue();
            TestScene.GetTree().Paused.ShouldBeTrue();
            hud._Input(escape);
            TestScene.GetTree().Paused.ShouldBeFalse();
        }
        finally
        {
            arena.Free();
        }
    }

    /// <summary>Title-screen reference works without granting a starter kit or needing a running combat session.</summary>
    [Test]
    public void TitleReferenceDoesNotCreateSessionItems()
    {
        var screen = new MainMenuScreen();
        TestScene.AddChild(screen);

        try
        {
            screen.GetNode<Button>("Menu/Design/Reference").EmitSignal(BaseButton.SignalName.Pressed);
            var panel = screen.GetNode<ReferencePanel>("ReferenceRoot/Design/Reference");
            panel.Visible.ShouldBeTrue();
            var list = (VBoxContainer)panel.FindChild("Entries", true, false);
            list.GetChildren()
                .OfType<Button>()
                .Any(button => button.GetMeta("reference_id").AsString().StartsWith("item.")).ShouldBeFalse();
            panel.Close();
            screen.GetNode<ColorRect>("ReferenceRoot/Design/Shade").Visible.ShouldBeFalse();
        }
        finally
        {
            screen.Free();
        }
    }

    private GreyboxArena CreateArena()
    {
        var runtime = TestScene.GetTree().Root.GetChildren()
            .OfType<GameBootstrap>()
            .Single();
        var arena = GD.Load<PackedScene>("res://Arena.tscn").Instantiate<GreyboxArena>();
        runtime.AddChild(arena);
        arena.Player.Controls.HardwareInput = false;
        arena.Opponent.Controls.ThinkingEnabled = false;

        return arena;
    }

    private static void Search(ReferencePanel panel, string query)
    {
        var search = panel.GetNode<LineEdit>("Book/Search");
        search.Text = query;
        search.EmitSignal(LineEdit.SignalName.TextChanged, query);
    }

    private static void Select(ReferencePanel panel, string id)
    {
        panel.ShowEntry(id);
    }
}
