using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Chickensoft.GoDotTest;
using Godot;
using RollForHonor.Domain.Combat.Attacks.Models;
using RollForHonor.Domain.Combat.Damage.Models;
using RollForHonor.Domain.Combat.Resolution.Models;
using RollForHonor.Domain.Combat.State.Models;
using RollForHonor.Domain.Dice.Models;
using RollForHonor.Godot.Arena;
using RollForHonor.Godot.UI.Common;
using RollForHonor.Godot.UI.Hud;
using Shouldly;

namespace RollForHonor.Godot.Tests;

/// <summary>Checks native floating damage colors, typography, translations, pause and expiry.</summary>
public sealed class GodotFloatingCombatTextTest : TestClass
{
    /// <summary>Creates presentation checks within the engine test host.</summary>
    /// <param name="testScene">The native test scene.</param>
    public GodotFloatingCombatTextTest(Node testScene) : base(testScene) { }

    /// <summary>Typed amounts stay separate, glances fade, crits use a bold font, and localized misses expire on game time.</summary>
    /// <returns>Completes after native pause and label lifetime checks.</returns>
    [Test]
    public async Task TypedResultsUseOutcomeStylingAndLocalizedMisses()
    {
        var runtime = TestScene.GetTree().Root.GetChildren()
            .OfType<GameBootstrap>()
            .Single();
        var arena = GD.Load<PackedScene>("res://Arena.tscn").Instantiate<GreyboxArena>();
        runtime.AddChild(arena);
        arena.Player.Controls.HardwareInput = false;
        arena.Opponent.Controls.ThinkingEnabled = false;
        var numbers = (FloatingCombatText)arena.GetNode<ArenaHud>("Presentation").FindChild("DamageNumbers", true, false);
        arena.Opponent.Position = arena.Player.Position + Vector3.Left * 4;
        var position = arena.Opponent.GlobalPosition + Vector3.Up * 1.8f;
        var locale = TranslationServer.GetLocale();

        try
        {
            numbers.ShowResult(position, Result(AttackOutcome.Hit));
            var labels = numbers.GetChildren().OfType<Label>().ToArray();
            labels.Select(label => label.Text).ShouldBe(["7", "3"]);
            labels[0].GetThemeColor("font_color").ShouldBe(CombatGlossary.DamageColor(DamageType.Fire));
            labels[1].GetThemeColor("font_color").ShouldBe(CombatGlossary.DamageColor(DamageType.Cold));
            labels[0].Position.ShouldNotBe(labels[1].Position);
            labels.All(label => label.MouseFilter == Control.MouseFilterEnum.Ignore).ShouldBeTrue();
            await Capture("hit.png");
            numbers._Process(2);
            numbers.GetChildCount().ShouldBe(0);
            numbers.ShowResult(position, Result(AttackOutcome.GlancingHit));
            var glance = numbers.GetChild<Label>(0);
            glance.GetThemeColor("font_color").A.ShouldBeLessThan(1);
            ((SystemFont)glance.GetThemeFont("font")).FontWeight.ShouldBe(400);
            await Capture("glance.png");
            numbers._Process(2);
            numbers.ShowResult(position, Result(AttackOutcome.Hit, independentCritical: true));
            ((SystemFont)numbers.GetChild<Label>(0).GetThemeFont("font")).FontWeight.ShouldBe(700);
            await Capture("critical.png");
            numbers._Process(2);
            numbers.ShowResult(position, Result(AttackOutcome.GlancingHit, independentCritical: true));
            var glancingCritical = numbers.GetChild<Label>(0);
            ((SystemFont)glancingCritical.GetThemeFont("font")).FontWeight.ShouldBe(700);
            glancingCritical.GetThemeColor("font_color").A.ShouldBeLessThan(1);
            numbers._Process(2);

            foreach (var language in new[] { "uk", "en" })
            {
                TranslationServer.SetLocale(language);
                numbers.ShowResult(position, Result(AttackOutcome.Evaded));
                var miss = numbers.GetChild<Label>(0);
                miss.Text.ShouldBe(language == "uk" ? "Промах" : "Miss");
                miss.GetThemeColor("font_color").ShouldBe(new Color("969791"));
                await Capture($"miss-{language}.png");
                var before = miss.Position;
                TestScene.GetTree().Paused = true;

                for (var frame = 0; frame < 5; frame++)
                {
                    await TestScene.ToSignal(TestScene.GetTree(), SceneTree.SignalName.PhysicsFrame);
                }

                miss.Position.ShouldBe(before);
                TestScene.GetTree().Paused = false;
                numbers._Process(0.9);
                miss.Position.Y.ShouldBeLessThan(before.Y);
                miss.Modulate.A.ShouldBeLessThan(1);
                numbers._Process(0.4);
                numbers.GetChildCount().ShouldBe(0);
            }

            numbers.ShowResult(position, Result(null, []));
            numbers.GetChildCount().ShouldBe(0);
        }
        finally
        {
            TestScene.GetTree().Paused = false;
            TranslationServer.SetLocale(locale);
            arena.Free();
        }
    }

    private static TargetCombatResult Result(AttackOutcome? outcome, DamageAmount[] amounts = null, bool independentCritical = false)
    {
        var packet = new DamagePacket(amounts ?? [new(DamageType.Fire, 5), new(DamageType.Fire, 2), new(DamageType.Cold, 3)]);
        var step = independentCritical
            ? new DamageStep(DamageStepType.CriticalCheck, packet, packet, 2, flatBonus: 0,
                decisionRoll: new DiceRoll(new(1, 20), [20], 20))
            : new DamageStep(DamageStepType.Finalization, packet, packet);
        var stage = new DamageCalculationResult(new DamageBreakdown([step]), packet);
        return new(new CombatantId(Guid.NewGuid()), null, outcome, new(stage, stage), [], false);
    }

    private async Task Capture(string name)
    {
        var directory = OS.GetEnvironment("RFH_FLOAT_CAPTURE_DIRECTORY");

        if (!string.IsNullOrEmpty(directory))
        {
            await TestScene.ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
            TestScene.GetViewport().GetTexture().GetImage().SavePng(Path.Combine(directory, name)).ShouldBe(Error.Ok);
        }
    }
}
