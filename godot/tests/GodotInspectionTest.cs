using System;
using System.Linq;
using System.Threading.Tasks;
using Chickensoft.GoDotTest;
using Godot;
using RollForHonor.Godot.Arena;
using RollForHonor.Godot.UI.Common;
using RollForHonor.Godot.UI.Hud;
using RollForHonor.Godot.UI.Inventory;
using Shouldly;

namespace RollForHonor.Godot.Tests;

/// <summary>Verifies linked combat explanations, branch ownership and interactive hover inspection.</summary>
public sealed class GodotInspectionTest : TestClass
{
    /// <summary>Creates native explanation tests.</summary>
    /// <param name="testScene">The engine test host.</param>
    public GodotInspectionTest(Node testScene) : base(testScene)
    {
    }

    /// <summary>Item links open nested defenses; closing a parent removes its descendants and Escape dismisses tips first.</summary>
    [Test]
    public void ItemLinksNavigateAndDismissTheirOwnBranch()
    {
        var arena = CreateArena();

        try
        {
            var hud = arena.GetNode<ArenaHud>("Presentation");
            hud.ToggleInventory();
            hud.FindChild("ashen-longbow", true, false).EmitSignal(BaseButton.SignalName.Pressed);
            var properties = hud.FindChild("ItemDetails", true, false).GetNode<RichTextLabel>("Card/Properties");
            properties.MetaUnderlined.ShouldBeTrue();
            properties.EmitSignal(RichTextLabel.SignalName.MetaClicked, "0");
            var stack = hud.GetNode<InspectionStack>("Root/Design/Inspections");
            stack.Count.ShouldBe(1);
            stack.GetNode<RichTextLabel>("Level0/Card/Body").GetParsedText().ShouldContain("6–11");
            stack.GetNode<RichTextLabel>("Level0/Card/Body").GetParsedText().ShouldContain("1d6 + 5");
            stack.GetNode<RichTextLabel>("Level0/Card/Body").EmitSignal(RichTextLabel.SignalName.MetaClicked, "1");
            stack.Count.ShouldBe(2);
            stack.GetNode<Label>("Level1/Card/Title").Text.ShouldBe("Armor");
            stack.GetNode<RichTextLabel>("Level1/Card/Body").EmitSignal(RichTextLabel.SignalName.MetaClicked, "0");
            stack.Count.ShouldBe(3);
            stack.GetNode<Button>("Level1/Card/Close").EmitSignal(BaseButton.SignalName.Pressed);
            stack.Count.ShouldBe(1);
            using var escape = new InputEventKey { PhysicalKeycode = Key.Escape, Pressed = true };
            hud._Input(escape);
            stack.Count.ShouldBe(0);
            ((InventoryPanel)hud.FindChild("Inventory", true, false)).Visible.ShouldBeTrue();
            TestScene.GetTree().Paused.ShouldBeFalse();
        }
        finally
        {
            arena.Free();
        }
    }

    /// <summary>Selecting another link in a parent replaces only its descendant branch.</summary>
    [Test]
    public void ParentLinksReplaceDescendantsAndAffixesKeepCapturedFacts()
    {
        var arena = CreateArena();

        try
        {
            var hud = arena.GetNode<ArenaHud>("Presentation");
            hud.ToggleInventory();
            hud.FindChild("ashen-longbow", true, false).EmitSignal(BaseButton.SignalName.Pressed);
            var card = (ItemDetailsPanel)hud.FindChild("ItemDetails", true, false);
            card.SetDetailed(true);
            card.GetNode<RichTextLabel>("Card/Properties").EmitSignal(RichTextLabel.SignalName.MetaClicked, "0");
            var stack = hud.GetNode<InspectionStack>("Root/Design/Inspections");
            var root = stack.GetNode<RichTextLabel>("Level0/Card/Body");
            root.EmitSignal(RichTextLabel.SignalName.MetaClicked, "0");
            stack.GetNode<Label>("Level1/Card/Title").Text.ShouldBe("Piercing resistance");
            root.EmitSignal(RichTextLabel.SignalName.MetaClicked, "1");
            stack.Count.ShouldBe(2);
            stack.GetNode<Label>("Level1/Card/Title").Text.ShouldBe("Armor");
            card.SetDetailed(false);
            root.EmitSignal(RichTextLabel.SignalName.MetaClicked, "2");
            stack.Count.ShouldBe(2);
            stack.GetNode<RichTextLabel>("Level1/Card/Body").GetParsedText().ShouldContain("[1/3] 72% T1");
            stack.GetNode<RichTextLabel>("Level1/Card/Body").MetaUnderlined.ShouldBeTrue();
            hud.ToggleInventory();
            stack.Count.ShouldBe(0);
        }
        finally
        {
            arena.Free();
        }
    }

    /// <summary>Alt keeps the hovered card stationary and interactive after leaving its cell; opened explanations persist.</summary>
    /// <returns>Completes after the engine dispatches native modifier press and release events.</returns>
    [Test]
    public async Task AltAllowsEnteringTheCursorTooltip()
    {
        var arena = CreateArena();

        try
        {
            var hud = arena.GetNode<ArenaHud>("Presentation");
            hud.ToggleInventory();
            var cell = hud.FindChild("ashen-longbow", true, false);
            cell.EmitSignal(Control.SignalName.MouseEntered);
            var tooltip = (ItemDetailsPanel)hud.FindChild("ItemTooltip", true, false);
            using var alt = new InputEventKey { PhysicalKeycode = Key.Alt, Pressed = true };
            Input.ParseInputEvent(alt);
            await Frames(2);
            tooltip.IsInspecting.ShouldBeTrue();
            tooltip.MouseFilter.ShouldBe(Control.MouseFilterEnum.Stop);
            tooltip.Position = new Vector2(600, 200);
            tooltip._Process(0);
            tooltip.Position.ShouldBe(new Vector2(600, 200));
            tooltip.Position = new Vector2(600, 1000);
            tooltip._Process(0);
            tooltip.GetRect().End.Y.ShouldBeLessThanOrEqualTo(tooltip.GetParent<Control>().Size.Y);
            cell.EmitSignal(Control.SignalName.MouseExited);
            tooltip.Visible.ShouldBeTrue();
            tooltip.GetNode<RichTextLabel>("Card/Properties").EmitSignal(RichTextLabel.SignalName.MetaClicked, "0");
            using var release = new InputEventKey { PhysicalKeycode = Key.Alt, Pressed = false };
            Input.ParseInputEvent(release);
            await Frames(2);
            tooltip.Visible.ShouldBeFalse();
            tooltip.MouseFilter.ShouldBe(Control.MouseFilterEnum.Ignore);
            hud.GetNode<InspectionStack>("Root/Design/Inspections").Count.ShouldBe(1);
        }
        finally
        {
            using var release = new InputEventKey { PhysicalKeycode = Key.Alt, Pressed = false };
            Input.ParseInputEvent(release);
            Input.ActionRelease("rfh_details");
            arena.Free();
        }
    }

    /// <summary>Resistance names link to the correct typed explanation with the actual current percentage.</summary>
    [Test]
    public void CharacterResistanceLinksUseTheirSemanticColorAndValue()
    {
        var arena = CreateArena();

        try
        {
            var hud = arena.GetNode<ArenaHud>("Presentation");
            hud.ToggleInventory();
            hud.FindChild("cinder-band", true, false).EmitSignal(BaseButton.SignalName.Pressed);
            hud.FindChild("ItemDetails", true, false).GetNode<Button>("Card/Action").EmitSignal(BaseButton.SignalName.Pressed);
            hud.ToggleCharacter();
            hud._Process(0);
            var character = hud.FindChild("Character", true, false);
            var links = character.FindChildren("*", "RichTextLabel", true, false)
                .Cast<RichTextLabel>()
                .ToArray();
            var fire = links.Single(link => link.GetParsedText() == "Fire");
            var damage = links.Single(link => link.GetParsedText() == "Fire damage");
            var fireValue = fire.GetParent().GetChild<Label>(1);
            fireValue.Text.ShouldBe("14%");
            fireValue.GetThemeColor("font_color").ShouldBe(damage.GetParent().GetChild<Label>(1).GetThemeColor("font_color"));
            fire.MetaUnderlined.ShouldBeTrue();
            fire.EmitSignal(RichTextLabel.SignalName.MetaClicked, "inspect");
            var stack = hud.GetNode<InspectionStack>("Root/Design/Inspections");
            stack.GetNode<Label>("Level0/Card/Title").Text.ShouldBe("Fire resistance");
            stack.GetNode<RichTextLabel>("Level0/Card/Body").GetParsedText().ShouldContain("14%");
        }
        finally
        {
            arena.Free();
        }
    }

    /// <summary>Nested cards and long descriptions stay inside the design area even when opened beside an edge.</summary>
    /// <returns>Completes after native text measurement and placement settle.</returns>
    [Test]
    public async Task NestedCardsStayInsideTheScreen()
    {
        var host = new Control { Size = new Vector2(1920, 1080) };
        TestScene.AddChild(host);
        var stack = new InspectionStack();
        host.AddChild(stack);

        try
        {
            stack.Open(CombatGlossary.Physical(), new Vector2(1910, 1070));
            stack.GetNode<RichTextLabel>("Level0/Card/Body").EmitSignal(RichTextLabel.SignalName.MetaClicked, "0");
            stack.GetNode<RichTextLabel>("Level1/Card/Body").EmitSignal(RichTextLabel.SignalName.MetaClicked, "0");

            await Frames(4);
            var panes = stack.GetChildren()
                .OfType<InspectionPane>()
                .ToArray();
            panes.Zip(panes.Skip(1))
                .All(pair => !pair.First.GetRect().Intersects(pair.Second.GetRect()))
                .ShouldBeTrue();
            host.Size = new Vector2(900, 600);
            await Frames(3);

            foreach (var pane in panes)
            {
                pane.Position.X.ShouldBeGreaterThanOrEqualTo(16);
                pane.Position.Y.ShouldBeGreaterThanOrEqualTo(16);
                pane.GetRect().End.X.ShouldBeLessThanOrEqualTo(host.Size.X - 16);
                pane.GetRect().End.Y.ShouldBeLessThanOrEqualTo(host.Size.Y - 16);
            }
        }
        finally
        {
            host.Free();
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

        ArenaItemFixtures.UseAuthoredKit(arena);

        return arena;
    }

    private async Task Frames(int count)
    {
        for (var frame = 0; frame < count; frame++)
        {
            await TestScene.ToSignal(TestScene.GetTree(), SceneTree.SignalName.ProcessFrame);
        }
    }
}
