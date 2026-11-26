using System;
using System.Linq;
using System.Threading.Tasks;
using Chickensoft.GoDotTest;
using Godot;
using RollForHonor.Godot;
using RollForHonor.Godot.Arena;
using RollForHonor.Godot.CombatDemo;
using RollForHonor.Godot.UI.Common;
using RollForHonor.Godot.UI.Hud;
using RollForHonor.Godot.UI.Inventory;
using Shouldly;

namespace RollForHonor.Godot.Tests;

/// <summary>Verifies independent pins, dragging and result-based combat inspection.</summary>
public sealed class GodotPinnedInspectionTest : TestClass
{
    /// <summary>Creates pinned-inspection integration checks.</summary>
    /// <param name="testScene">The engine test host.</param>
    public GodotPinnedInspectionTest(Node testScene) : base(testScene)
    {
    }

    /// <summary>A pinned child survives parent closure, new roots and closing both source windows.</summary>
    /// <returns>Completes after native layout and dragging settle.</returns>
    [Test]
    public async Task PinnedCardsSurviveWindowsAndRetainDraggedPositions()
    {
        var arena = CreateArena();

        try
        {
            var hud = arena.GetNode<ArenaHud>("Presentation");
            hud.ToggleInventory();
            hud.ToggleCharacter();
            var stack = hud.GetNode<InspectionStack>("Root/Design/Inspections");
            stack.Open(CombatGlossary.Physical(), new Vector2(500, 100));
            stack.GetNode<RichTextLabel>("Level0/Card/Body").EmitSignal(RichTextLabel.SignalName.MetaClicked, "0");
            await Frames(3);
            var retained = stack.GetNode<InspectionPane>("Level1");
            retained.GetNode<Button>("Card/Pin").EmitSignal(BaseButton.SignalName.Pressed);
            retained.IsPinned.ShouldBeTrue();
            var handle = retained.GetNode<Control>("Card/DragHandle");
            using var down = new InputEventMouseButton { ButtonIndex = MouseButton.Left, Pressed = true, Position = new Vector2(20, 20) };
            handle.EmitSignal(Control.SignalName.GuiInput, down);
            var before = retained.Position;
            using var motion = new InputEventMouseMotion { Position = new Vector2(-60, 60) };
            handle.EmitSignal(Control.SignalName.GuiInput, motion);
            using var up = new InputEventMouseButton { ButtonIndex = MouseButton.Left, Pressed = false };
            handle.EmitSignal(Control.SignalName.GuiInput, up);
            retained.Position.ShouldBe(before + new Vector2(-80, 40));
            var moved = retained.Position;
            stack.GetNode<Button>("Level0/Card/Close").EmitSignal(BaseButton.SignalName.Pressed);
            stack.Count.ShouldBe(1);
            hud.ToggleInventory();
            hud.ToggleCharacter();
            await Frames(3);
            retained.IsVisibleInTree().ShouldBeTrue();
            retained.Position.ShouldBe(moved);
            retained.GetNode<RichTextLabel>("Card/Body").EmitSignal(RichTextLabel.SignalName.MetaClicked, "0");
            stack.Count.ShouldBe(2);
            stack.CloseLast().ShouldBeTrue();
            stack.Open(CombatGlossary.Armor(), new Vector2(200, 100));
            stack.Count.ShouldBe(2);
            stack.CloseLast().ShouldBeTrue();
            stack.Count.ShouldBe(1);
            stack.CloseLast().ShouldBeFalse();
            retained.GetNode<Button>("Card/Pin").EmitSignal(BaseButton.SignalName.Pressed);
            stack.Clear();
            stack.Count.ShouldBe(0);
        }
        finally
        {
            arena.Free();
        }
    }

    /// <summary>Affix-only stats appear once and never masquerade as implicit base properties.</summary>
    [Test]
    public void AffixOnlyDamageIsNotDuplicatedInBaseProperties()
    {
        var arena = CreateArena();

        try
        {
            var hud = arena.GetNode<ArenaHud>("Presentation");
            hud.ToggleInventory();
            hud.FindChild("oathkeeper-ring", true, false).EmitSignal(BaseButton.SignalName.Pressed);
            var text = hud.FindChild("ItemDetails", true, false).GetNode<RichTextLabel>("Card/Properties").GetParsedText();
            text.Split("increased all damage").Length.ShouldBe(2);
            text[..text.IndexOf("AFFIXES", StringComparison.Ordinal)].ShouldNotContain("all damage");
            text.ShouldContain("+5 maximum mana");
        }
        finally
        {
            arena.Free();
        }
    }

    /// <summary>The d20 control opens nested roll history, and journal links retain a real committed attack's arithmetic.</summary>
    /// <returns>Completes after actual physics contact commits the sample attack.</returns>
    [Test]
    public async Task DiceAndJournalUseTheSameRetainedCombatFacts()
    {
        var arena = CreateArena();
        var runtime = arena.GetParent<GameBootstrap>();
        var combat = GD.Load<PackedScene>("res://Game.tscn").Instantiate<CombatDemoScene>();
        combat.Position = new Vector3(0, 0, 200);
        runtime.AddChild(combat);

        try
        {
            var hud = arena.GetNode<ArenaHud>("Presentation");
            var history = hud.GetNode<ActionJournalPanel>("Root/Design/History");
            var input = runtime.CombatState.Read(combat.Source.CombatantId, combat.Target.CombatantId);
            combat.Fire();

            for (var frame = 0; frame < 180 && combat.LastContact is null; frame++)
            {
                await TestScene.ToSignal(TestScene.GetTree(), SceneTree.SignalName.PhysicsFrame);
            }

            combat.LastContact.ShouldNotBeNull();
            history.Record("Player", combat.LastContact, input, "Target");
            hud._Process(0);
            var dice = hud.GetNode<DiceRollControl>("Root/Design/ActionBar/RollValue");
            var number = dice.GetNode<Label>("Value");
            number.Text.ShouldBe(history.LastRoll.ToString());
            number.HorizontalAlignment.ShouldBe(HorizontalAlignment.Center);
            number.VerticalAlignment.ShouldBe(VerticalAlignment.Center);
            number.GetRect().GetCenter().ShouldBe(new Vector2(48, 40));
            dice.EmitSignal(Control.SignalName.MouseEntered);
            ((FloatingTip)hud.FindChild("TextTooltip", true, false)).Visible.ShouldBeFalse();
            using var click = new InputEventMouseButton { ButtonIndex = MouseButton.Left, Pressed = true };
            dice._GuiInput(click);
            var stack = hud.GetNode<InspectionStack>("Root/Design/Inspections");
            stack.GetNode<Label>("Level0/Card/Title").Text.ShouldBe("Recent rolls");
            stack.GetNode<RichTextLabel>("Level0/Card/Body").EmitSignal(RichTextLabel.SignalName.MetaClicked, "0");
            stack.GetNode<RichTextLabel>("Level1/Card/Body").GetParsedText().ShouldContain("1d20");
            var retained = stack.GetNode<InspectionPane>("Level1");
            retained.GetNode<Button>("Card/Pin").EmitSignal(BaseButton.SignalName.Pressed);
            history.GetNode<RichTextLabel>("Journal/Entries").EmitSignal(RichTextLabel.SignalName.MetaClicked, "1");
            stack.Count.ShouldBe(2);
            stack.GetNode<Label>("Level0/Card/Title").Text.ShouldBe("Damage breakdown");
            var breakdown = stack.GetNode<RichTextLabel>("Level0/Card/Body");
            breakdown.GetParsedText().ShouldContain("Armor reduction");
            breakdown.EmitSignal(RichTextLabel.SignalName.MetaClicked, "0");
            stack.Count.ShouldBe(3);
            history.CreateDiceHistoryTopic().Links.Count.ShouldBe(1);
        }
        finally
        {
            combat.Free();
            arena.Free();
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
