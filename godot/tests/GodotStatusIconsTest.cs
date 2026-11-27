using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Chickensoft.GoDotTest;
using Godot;
using RollForHonor.Domain.Combat.Effects.Models;
using RollForHonor.Godot.Arena;
using RollForHonor.Godot.Content.Combat;
using RollForHonor.Godot.Gameplay.Combat;
using RollForHonor.Godot.Gameplay.Targeting;
using RollForHonor.Godot.UI.Common;
using RollForHonor.Godot.UI.Hud;
using Shouldly;

namespace RollForHonor.Godot.Tests;

/// <summary>Verifies selected-target status icons, native detail clicks and canonical selection/expiry refresh.</summary>
public sealed class GodotStatusIconsTest : TestClass
{
    /// <summary>Creates status-icon checks in the native engine host.</summary>
    /// <param name="testScene">The test host.</param>
    public GodotStatusIconsTest(Node testScene) : base(testScene) { }

    /// <summary>Icons group poison stacks, open captured localized details and refresh when effects or target change.</summary>
    /// <returns>Completes after actual mouse routing and rendered inspection layout.</returns>
    [Test]
    public async Task ActiveIconsOpenCapturedDetailsAndFollowSelection()
    {
        var runtime = TestScene.GetTree().Root.GetChildren()
            .OfType<GameBootstrap>()
            .Single();
        var arena = GD.Load<PackedScene>("res://Arena.tscn").Instantiate<GreyboxArena>();
        runtime.AddChild(arena);
        arena.Player.Controls.HardwareInput = false;
        arena.Opponent.Controls.ThinkingEnabled = false;
        arena.Opponent.Position = arena.Player.Position + Vector3.Left * 4;
        var hud = arena.GetNode<ArenaHud>("Presentation");
        var row = hud.GetNode<TargetStatusIcons>("Root/Design/Enemy/Statuses");
        var target = arena.Opponent.CombatantId;
        var state = runtime.CombatState;
        var previousLocale = TranslationServer.GetLocale();
        TranslationServer.SetLocale("uk");

        try
        {
            var source = state.Read(arena.Player.CombatantId, target).Source;
            var effects = StatusEffectContent.Rules
                .SelectMany(rule => Enumerable.Range(0, rule.Kind == StatusEffectKind.Poison ? 2 : 1)
                    .Select(_ => new CombatEffectSnapshot(new(Guid.NewGuid()),
                        new PeriodicDamageEffectDefinition(rule.Id, [new(new(2, 4), rule.FlatDamagePerDie * 2, rule.DamageType)],
                            tickInterval: TimeSpan.FromSeconds(0.5), tickCount: 12, damageMultiplier: 1.5m,
                            movementSlow: rule.Kind == StatusEffectKind.Bleed ? 0.2m : 0), source)))
                .ToArray();
            var before = state.Read(target, target).Target;
            state.UpdateCombatant(new(target, before.Health, before.MaxHealth, before.Armor, before.Evasion,
                before.Resistances, effects, before.Offense, before.EvasionRating));
            var selection = arena.Player.GetRequiredComponent<CombatAwareness>().Selection;
            selection.Select(target);
            hud._Process(0);
            await Frames(3);
            row.GetChildren().OfType<Button>().Count(button => button.Visible).ShouldBe(3);
            row.GetNode<Label>("Poison/Count").Text.ShouldBe("2");
            row.GetNode<Label>("Ignite/Count").Text.ShouldBeEmpty();
            row.GetNode<Button>("Poison").TooltipText.ShouldContain("Отруєння");
            hud.GetNode<Panel>("Root/Design/Enemy").Size.Y.ShouldBe(122);
            await Capture("status-icons-uk.png");

            var version = state.Read(target, target).Version;
            var button = row.GetNode<Button>("Bleed");
            var point = button.GetGlobalTransformWithCanvas() * (button.Size / 2);
            TestScene.GetViewport().PushInput(new InputEventMouseMotion { Position = point, GlobalPosition = point }, true);
            TestScene.GetViewport().PushInput(new InputEventMouseButton
            {
                ButtonIndex = MouseButton.Left,
                Pressed = true,
                Position = point,
                GlobalPosition = point
            }, true);
            TestScene.GetViewport().PushInput(new InputEventMouseButton
            {
                ButtonIndex = MouseButton.Left,
                Pressed = false,
                Position = point,
                GlobalPosition = point
            }, true);
            await Frames(3);
            var inspector = hud.GetNode<InspectionStack>("Root/Design/Inspections");
            inspector.Count.ShouldBe(1);
            var pane = inspector.GetChildren().OfType<InspectionPane>().Single();
            pane.GetNode<Label>("Card/Title").Text.ShouldBe("Кровотеча");
            var details = pane.GetNode<RichTextLabel>("Card/Body").GetParsedText();
            details.ShouldContain("2d4 - 2");
            details.ShouldContain("0.5 с");
            details.ShouldContain("6 с");
            details.ShouldContain("20%");
            details.ShouldContain("×1.5");
            state.Read(target, target).Version.ShouldBe(version);
            selection.TargetId.ShouldBe(target);
            await Capture("status-details-uk.png");

            TranslationServer.SetLocale("en");
            hud._Process(0);
            row.GetNode<Button>("Poison").TooltipText.ShouldContain("Poisoned");
            row.GetNode<Button>("Poison").EmitSignal(Button.SignalName.Pressed);
            inspector.Count.ShouldBe(1);
            inspector.GetChildren().OfType<InspectionPane>().Single()
                .GetNode<RichTextLabel>("Card/Body").GetParsedText().ShouldContain("Active stacks: 2");

            selection.Select(arena.Player.CombatantId);
            hud._Process(0);
            row.GetChildren().OfType<Button>().All(value => !value.Visible).ShouldBeTrue();
            hud.GetNode<Panel>("Root/Design/Enemy").Size.Y.ShouldBe(80);
            selection.Select(target);
            hud._Process(0);
            row.GetNode<Button>("Poison").Visible.ShouldBeTrue();
            state.UpdateCombatant(new(target, before.Health, before.MaxHealth, before.Armor, before.Evasion,
                before.Resistances, [], before.Offense, before.EvasionRating));
            hud._Process(0);
            row.GetChildren().OfType<Button>().All(value => !value.Visible).ShouldBeTrue();
            TranslationServer.SetLocale("uk");
            hud._Process(0);
            await Capture("status-compact-uk.png");
            selection.Select(null);
            hud._Process(0);
            hud.GetNode<Panel>("Root/Design/Enemy").Visible.ShouldBeFalse();
        }
        finally
        {
            TranslationServer.SetLocale(previousLocale);
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

    private async Task Capture(string name)
    {
        var directory = OS.GetEnvironment("RFH_STATUS_CAPTURE_DIRECTORY");

        if (!string.IsNullOrEmpty(directory))
        {
            await TestScene.ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
            TestScene.GetViewport().GetTexture().GetImage().SavePng(Path.Combine(directory, name)).ShouldBe(Error.Ok);
        }
    }
}
