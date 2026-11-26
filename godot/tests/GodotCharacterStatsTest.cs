using System.Linq;
using System.Threading.Tasks;
using Chickensoft.GoDotTest;
using Godot;
using RollForHonor.Application.Equipment.Models;
using RollForHonor.Application.Equipment.Services;
using RollForHonor.Domain.Items.Configuration;
using RollForHonor.Domain.Items.Equipment.Models;
using RollForHonor.Domain.Items.Models;
using RollForHonor.Domain.Items.Services;
using RollForHonor.Domain.Stats;
using RollForHonor.Godot.Arena;
using RollForHonor.Godot.Gameplay.Movement;
using RollForHonor.Godot.Gameplay.Stats;
using RollForHonor.Godot.UI.Common;
using RollForHonor.Godot.UI.Hud;
using RollForHonor.Godot.UI.Inventory;
using Shouldly;

namespace RollForHonor.Godot.Tests;

/// <summary>Checks editable stat definitions and live shared-actor projection in the actual engine.</summary>
public sealed class GodotCharacterStatsTest : TestClass
{
    private GameBootstrap Runtime => TestScene.GetTree().Root.GetChildren()
        .OfType<GameBootstrap>()
        .Single();

    /// <summary>Creates engine-level stat checks.</summary>
    /// <param name="testScene">The host used to await native physics.</param>
    public GodotCharacterStatsTest(Node testScene) : base(testScene)
    {
    }

    /// <summary>Player and enemy register from separate resources through the same immutable Domain model.</summary>
    /// <returns>Completes after initial stat bridges have advanced.</returns>
    [Test]
    public async Task DefinitionsUseApprovedPlayerAndMigratedEnemyRatings()
    {
        var arena = Create();

        try
        {
            await Frames(3);
            var player = Runtime.CombatState.Read(arena.Player.CombatantId, arena.Opponent.CombatantId);

            player.Source.MaxHealth.ShouldBe(100);
            player.Source.Armor.ShouldBe(0);
            player.Source.Evasion.ShouldBe(0);
            player.Target.MaxHealth.ShouldBe(40);
            player.Target.Evasion.ShouldBe(40);
            arena.Player.GetRequiredComponent<MovementMotor>().Speed.ShouldBe(6);
            arena.Opponent.GetRequiredComponent<MovementMotor>().Speed.ShouldBe(3);
            arena.Player.GetRequiredComponent<ActorStats>().Definition.ShouldNotBeSameAs(
                arena.Opponent.GetRequiredComponent<ActorStats>().Definition);
        }
        finally
        {
            arena.Free();
        }
    }

    /// <summary>A runtime modifier updates speed and combat attributes without mutating the shared definition resource.</summary>
    /// <returns>Completes after the motor consumes the newly published resolved values.</returns>
    [Test]
    public async Task LiveModifiersReachMotorAndCombatProjection()
    {
        var arena = Create();

        try
        {
            var id = arena.Player.CombatantId;
            var profile = Runtime.CombatState.GetStatProfile(id);
            Runtime.CombatState.UpdateStats(id, new(profile.BaseValues,
            [
                new(new(StatType.MovementSpeed), StatOperation.Increased, 0.5m, "boots:test"),
                new(new(StatType.Armor), StatOperation.Flat, 10, "armor:test"),
                new(new(StatType.MaxHealth), StatOperation.Flat, 50, "armor:test")
            ]));
            await Frames(3);
            var state = Runtime.CombatState.Read(id, id).Source;

            arena.Player.GetRequiredComponent<MovementMotor>().Speed.ShouldBe(9);
            state.Armor.ShouldBe(10);
            state.MaxHealth.ShouldBe(150);
            state.Health.ShouldBe(100);
            arena.Player.GetRequiredComponent<ActorStats>().Definition.MovementSpeed.ShouldBe(6);
        }
        finally
        {
            arena.Free();
        }
    }

    /// <summary>Application equip/unequip changes the actual actor motor and combat projection without a second engine calculation.</summary>
    /// <returns>Completes after the motor consumes both published equipment states.</returns>
    [Test]
    public async Task EquipmentChangesReachTheActorMotorAndCombatState()
    {
        var arena = Create();

        try
        {
            var id = arena.Player.CombatantId;
            var item = new ItemFactory(ItemRarityRules.Default, ItemStatRules.Default).Create("test-boots",
                new ItemDefinition("boots", "Boots", ["boots"],
                [
                    new ItemModifier(new StatKey(StatType.MovementSpeed), StatOperation.Increased, 0.5m),
                    new ItemModifier(new StatKey(StatType.Armor), StatOperation.Flat, 10)
                ]), ItemRarity.Common);
            Runtime.CombatState.RegisterOwnedItem(id, item);
            var equipment = new EquipmentService(Runtime.CombatState);

            equipment.EquipItem(id, item.Id, EquipmentSlot.Boots).ShouldBe(EquipmentChangeStatus.Applied);
            await Frames(3);

            arena.Player.GetRequiredComponent<MovementMotor>().Speed.ShouldBe(9);
            Runtime.CombatState.Read(id, id).Source.Armor.ShouldBe(10);
            equipment.UnequipItem(id, EquipmentSlot.Boots).ShouldBe(EquipmentChangeStatus.Applied);
            await Frames(3);

            arena.Player.GetRequiredComponent<MovementMotor>().Speed.ShouldBe(6);
            Runtime.CombatState.Read(id, id).Source.Armor.ShouldBe(0);
            arena.Player.GetRequiredComponent<ActorStats>().Definition.MovementSpeed.ShouldBe(6);
        }
        finally
        {
            arena.Free();
        }
    }

    /// <summary>The visible EHP and its inline damage-type details update from authoritative defenses.</summary>
    /// <returns>Completes after the native character panel is initialized.</returns>
    [Test]
    public async Task CharacterPanelDisplaysReferenceEffectiveHealth()
    {
        var arena = Create();

        try
        {
            await Frames(3);
            var id = arena.Player.CombatantId;
            var profile = Runtime.CombatState.ReadEquipment(id).NonEquipmentProfile;
            Runtime.CombatState.UpdateNonEquipmentStats(id,
                new(profile.BaseValues.With(new(StatType.Armor), 130), profile.Modifiers));
            var hud = arena.GetNode<ArenaHud>("Presentation");
            hud.ToggleCharacter();
            hud._Process(0);
            var panel = (CharacterPanel)hud.FindChild("Character", true, false);
            var value = (Label)panel.FindChild("EffectiveHealthValue", true, false);
            value.Text.ShouldBe("118.4");
            var capture = OS.GetEnvironment("RFH_EHP_CAPTURE_DIRECTORY");

            if (!string.IsNullOrWhiteSpace(capture))
            {
                await TestScene.ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
                TestScene.GetViewport().GetTexture().GetImage().SavePng(System.IO.Path.Combine(capture, "ehp.png")).ShouldBe(Error.Ok);
            }

            var link = (RichTextLabel)panel.FindChild("EffectiveHealthLink", true, false);
            link.GetParsedText().ShouldBe("EHP");
            link.TooltipText.ShouldBeEmpty();
            InspectionTopic opened = null;
            panel.InspectionRequested += topic => opened = topic;
            link.EmitSignal(RichTextLabel.SignalName.MetaClicked, "ehp");
            opened.ShouldNotBeNull();
            opened.Facts.Count.ShouldBe(30);
            opened.Links.Count.ShouldBe(28);
            opened.Facts.Count(fact => fact.LinkIndex is not null).ShouldBe(28);
            var mixed = opened.Links[7].Open();
            mixed.Links.Count.ShouldBe(4);
            mixed.Links[0].Open().Links.ShouldNotBeEmpty();
            var critical = (Label)panel.FindChild("CriticalMultiplierValue", true, false);
            critical.Text.ShouldBe("+100%");
            critical.GetGlobalRect().End.X.ShouldBeLessThanOrEqualTo(((Control)panel.FindChild("StatsScroll", true, false)).GetGlobalRect().End.X);
            ((RichTextLabel)critical.GetParent().GetChild(0)).EmitSignal(RichTextLabel.SignalName.MetaClicked, "inspect");
            opened.Links.ShouldNotBeEmpty();
            opened.Links[0].Open().Links.ShouldNotBeEmpty();
            var evasion = (Label)panel.FindChild("EvasionValue", true, false);
            ((RichTextLabel)evasion.GetParent().GetChild(0)).EmitSignal(RichTextLabel.SignalName.MetaClicked, "inspect");
            opened.Links.Count.ShouldBe(3);
            opened.Links[0].Open().Links.ShouldNotBeEmpty();
            var accuracy = (Label)panel.FindChild("AttackModifierValue", true, false);
            ((RichTextLabel)accuracy.GetParent().GetChild(0)).EmitSignal(RichTextLabel.SignalName.MetaClicked, "inspect");
            opened.Title.ShouldBe(TranslationServer.Translate("Accuracy"));
            opened.Links.ShouldNotBeEmpty();

            if (!string.IsNullOrWhiteSpace(capture))
            {
                link.EmitSignal(RichTextLabel.SignalName.MetaClicked, "ehp");
                await TestScene.ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
                TestScene.GetViewport().GetTexture().GetImage().SavePng(System.IO.Path.Combine(capture, "ehp-categories.png")).ShouldBe(Error.Ok);
            }
            Runtime.CombatState.UpdateNonEquipmentStats(id,
                new(profile.BaseValues.With(new(StatType.Evasion), 100), profile.Modifiers));
            hud._Process(0);
            value.Text.ShouldBe("135.7");
            Runtime.CombatState.UpdateNonEquipmentStats(id,
                new(profile.BaseValues.With(new(StatType.Armor), 45).With(new(StatType.Evasion), 45)
                    .With(new(StatType.CriticalMultiplier), 2.5m), profile.Modifiers));
            hud._Process(0);
            value.Text.ShouldBe("121.6");
            critical.Text.ShouldBe("+150%");
        }
        finally
        {
            arena.Free();
        }
    }

    private GreyboxArena Create()
    {
        var arena = GD.Load<PackedScene>("res://Arena.tscn").Instantiate<GreyboxArena>();
        Runtime.AddChild(arena);
        arena.Player.Controls.HardwareInput = false;
        arena.Opponent.Controls.ThinkingEnabled = false;

        return arena;
    }

    private async Task Frames(int count)
    {
        for (var frame = 0; frame < count; frame++)
        {
            await TestScene.ToSignal(TestScene.GetTree(), SceneTree.SignalName.PhysicsFrame);
        }
    }
}
