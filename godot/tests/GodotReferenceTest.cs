using System.Linq;
using Chickensoft.GoDotTest;
using Godot;
using RollForHonor.Domain.Stats;
using RollForHonor.Godot.Arena;
using RollForHonor.Godot.UI.Common;
using RollForHonor.Godot.UI.Hud;
using RollForHonor.Godot.UI.Inventory;
using RollForHonor.Godot.UI.Menu;
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
            entries.Single(entry => entry.Id == "stat.CriticalChance").Topic.Description.ShouldContain("not rolled");
            entries.Count(entry => entry.Category == "Owned items").ShouldBe(before.OwnedItems.Count);
            entries.Count(entry => entry.Category == "Rolled affixes").ShouldBe(before.OwnedItems.Values.Sum(item => item.Affixes.Count));
            var item = before.OwnedItems.Values.Single(item => item.Base.Id == "ashen-longbow");
            var actual = ItemInspectionTopics.Affix(item.Rarity, item.Affixes[0], true);
            var indexed = entries.Single(entry => entry.Id == $"affix.{item.Id.Length}:{item.Id}.{item.Affixes[0].Id}");
            indexed.Topic.Facts.ShouldBe(actual.Facts);
            indexed.Topic.Facts[0].Text.ShouldContain("72%");
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
            var search = panel.GetNode<LineEdit>("Book/Search");
            var category = panel.GetNode<OptionButton>("Book/Category");
            var list = (VBoxContainer)panel.FindChild("Entries", true, false);
            Search(panel, "FIRE resistance");
            list.GetChildCount().ShouldBe(entries.Count(entry => entry.Matches("FIRE resistance")));
            var index = Enumerable.Range(0, category.ItemCount).Single(value => category.GetItemText(value) == "Resistances");
            category.Select(index);
            category.EmitSignal(OptionButton.SignalName.ItemSelected, index);
            list.GetChildCount().ShouldBe(1);
            ((Button)list.GetChild(0)).GetMeta("reference_id").AsString().ShouldBe("resistance.Fire");
            Search(panel, "nothing-matches-this-query");
            list.GetChildCount().ShouldBe(0);
            panel.GetNode<Button>("Book/Inspect").Disabled.ShouldBeTrue();
            panel.GetNode<RichTextLabel>("Book/Article").GetParsedText().ShouldContain("Try another search");
            search.Text = "";
            panel.Open(entries);
            list.GetChildCount().ShouldBe(7);
        }
        finally
        {
            panel.Free();
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
        var list = (VBoxContainer)panel.FindChild("Entries", true, false);
        list.GetChildren()
            .OfType<Button>()
            .Single(button => button.GetMeta("reference_id").AsString() == id)
            .EmitSignal(BaseButton.SignalName.Pressed);
    }
}
