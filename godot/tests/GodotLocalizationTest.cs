using System;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Chickensoft.GoDotTest;
using Godot;
using RollForHonor.Domain.Combat.Damage.Models;
using RollForHonor.Godot.Content.Items;
using RollForHonor.Godot.UI.Inventory;
using RollForHonor.Godot.UI.Localization;
using RollForHonor.Godot.UI.Menu;
using RollForHonor.Godot.UI.Reference;
using RollForHonor.Godot.UI.Settings;
using RollForHonor.Godot.UI.Common;
using RollForHonor.Godot.UI.Hud;
using RollForHonor.Godot.Arena;
using RollForHonor.Domain.Items.Equipment.Models;
using RollForHonor.Application.Equipment.Services;
using Shouldly;

namespace RollForHonor.Godot.Tests;

/// <summary>Verifies Ukrainian content, native UI identities, search and numeric facts against the imported catalog.</summary>
public sealed class GodotLocalizationTest : TestClass
{
    /// <summary>Creates localization checks in the engine host.</summary>
    /// <param name="testScene">The engine test scene.</param>
    public GodotLocalizationTest(Node testScene) : base(testScene)
    {
    }

    /// <summary>Translated menu labels retain node paths; Ukrainian reference search finds authored and live item entries.</summary>
    [Test]
    public void UkrainianMenuAndReferencePreserveIdentitiesAndSearch()
    {
        var previous = TranslationServer.GetLocale();
        TranslationServer.SetLocale("uk");
        var menu = new MainMenuView();
        var reference = new ReferencePanel();
        TestScene.AddChild(menu);
        TestScene.AddChild(reference);

        try
        {
            menu.GetNode<Button>("Design/Start").Text.ShouldBe("Почати гру");
            menu.GetNode<Button>("Design/Reference").Text.ShouldBe("Довідник");
            var items = StarterItemCatalog.Create(new(Guid.NewGuid()));
            var bow = items.Single(item => item.Base.Id == "ashen-longbow");
            bow.Base.Name.ShouldBe("Ashen Longbow");
            var catalog = ReferenceCatalog.Create(items);
            var entry = catalog.Single(value => value.Id == $"item.{bow.Id}");
            entry.Topic.Title.ShouldBe("Попелястий довгий лук");
            entry.Matches("ПОПЕЛЯСТИЙ лук").ShouldBeTrue();
            catalog.Single(value => value.Id == "combat.attack-roll").Matches("кидки атаки").ShouldBeTrue();
            reference.Open(catalog);
            reference.SelectSection("items");
            var search = reference.GetNode<LineEdit>("Book/Search");
            search.Text = "дерев’яний лук";
            search.EmitSignal(LineEdit.SignalName.TextChanged, search.Text);
            reference.GetNode<ReferenceTableView>("Book/Table").VisibleRowCount.ShouldBe(5);
            catalog.Single(value => value.Id == "loot.bases").Table.Rows
                .Where(row => row.Matches("дерев’яний лук", ""))
                .All(row => L.Refresh(row.Cells[0]).Contains("дерев’яний лук", StringComparison.OrdinalIgnoreCase)).ShouldBeTrue();
            var affix = ItemInspectionTopics.Affix(bow.Rarity, bow.Affixes[0], true);
            affix.Title.ShouldContain("Тверда рука");
            affix.Facts.Any(fact => fact.Text.Contains("72%", StringComparison.Ordinal)).ShouldBeTrue();
        }
        finally
        {
            reference.Free();
            menu.Free();
            TranslationServer.SetLocale(previous);
        }
    }

    /// <summary>Every authored reference paragraph, item name and affix label has a Ukrainian translation.</summary>
    [Test]
    public void AuthoredContentHasCompleteUkrainianCoverage()
    {
        var previous = TranslationServer.GetLocale();
        TranslationServer.SetLocale("uk");

        try
        {
            using var reference = JsonDocument.Parse(FileAccess.GetFileAsString("res://Content/Reference/topics.json"));
            var sources = reference.RootElement.EnumerateArray()
                .SelectMany(entry => new[] { "category", "title", "description" }
                    .Select(key => entry.GetProperty(key).GetString()));
            var items = StarterItemCatalog.Create(new(Guid.NewGuid()));
            sources = sources.Concat(items
                .Select(item => item.Base.Name))
                .Concat(items
                    .SelectMany(item => item.Affixes)
                    .Select(affix => affix.DefinitionId.Replace('-', ' ')));

            foreach (var source in sources.Distinct())
            {
                L.Text(source).ShouldNotBe(source, source);
            }
        }
        finally
        {
            TranslationServer.SetLocale(previous);
        }
    }

    /// <summary>Message formatting preserves captured numeric values and English fallback without altering combat models.</summary>
    [Test]
    public void TemplatesKeepNumericFactsAndEnglishFallback()
    {
        var previous = TranslationServer.GetLocale();

        try
        {
            TranslationServer.SetLocale("uk");
            L.Format($"Attack speed {1.25m:0.##}/s · Mana cost {10m:0.##}")
                .ShouldBe("Швидкість атаки 1.25/с · Мана 10");
            var component = new DamageComponent(new(1, 6), -3, DamageType.Piercing);
            L.Damage(component).ShouldBe("1d6 - 3 [Колюча]");
            component.ToString().ShouldBe("1d6 - 3 [Piercing]");
            L.Text("unregistered content").ShouldBe("unregistered content");
            TranslationServer.SetLocale("en");
            L.Text("Start game").ShouldBe("Start game");
            L.Damage(component).ShouldBe(component.ToString());
        }
        finally
        {
            TranslationServer.SetLocale(previous);
        }
    }

    /// <summary>Apply changes and persists the language immediately; closing the form discards an unapplied language.</summary>
    /// <returns>Completes after native controls receive translation-change notifications.</returns>
    [Test]
    public async Task SettingsApplyAndDiscardLanguageWithoutRecreatingTheMenu()
    {
        var previous = TranslationServer.GetLocale();
        var path = $"user://language-settings-{Guid.NewGuid():N}.cfg";
        TranslationServer.SetLocale("en");
        var menu = new MainMenuView();
        var settings = new SettingsPanel { PreferencesPath = path };
        TestScene.AddChild(menu);
        TestScene.AddChild(settings);

        try
        {
            settings.Open();
            var language = settings.GetNode<OptionButton>("Design/Settings/Language");
            language.Selected.ShouldBe(1);
            language.Select(0);
            settings.Close();
            TranslationServer.GetLocale().ShouldBe("en");
            FileAccess.FileExists(path).ShouldBeFalse();
            settings.Open();
            language.Selected.ShouldBe(1);
            language.Select(0);
            var start = menu.GetNode<Button>("Design/Start");
            settings.GetNode<Button>("Design/Settings/Apply").EmitSignal(BaseButton.SignalName.Pressed);
            await Frames(2);
            start.Text.ShouldBe("Почати гру");
            menu.GetNode<Button>("Design/Start").ShouldBeSameAs(start);
            settings.GetNode<OptionButton>("Design/Settings/WindowMode").GetItemText(0).ShouldBe("Віконний");
            new PreferencesStore(path).Load().Language.ShouldBe("uk");
            settings.Visible.ShouldBeTrue();
            language.Select(1);
            settings.GetNode<Button>("Design/Settings/Apply").EmitSignal(BaseButton.SignalName.Pressed);
            await Frames(2);
            start.Text.ShouldBe("Start game");
            new PreferencesStore(path).Load().Language.ShouldBe("en");
        }
        finally
        {
            settings.Free();
            menu.Free();
            DirAccess.RemoveAbsolute(ProjectSettings.GlobalizePath(path));
            TranslationServer.SetLocale(previous);
        }
    }

    /// <summary>Invalid or missing stored locales fall back safely; a failed save leaves the active language unchanged.</summary>
    [Test]
    public void InvalidLanguageAndFailedSaveDoNotChangeTheRuntime()
    {
        var previous = TranslationServer.GetLocale();
        var path = $"user://language-validation-{Guid.NewGuid():N}.cfg";

        try
        {
            var store = new PreferencesStore(path);
            store.Load().Language.ShouldBe("uk");
            using var config = new ConfigFile();
            config.SetValue("presentation", "language", "unknown");
            config.Save(path).ShouldBe(Error.Ok);
            store.Load().Language.ShouldBe("uk");
            config.SetValue("presentation", "language", 123);
            config.Save(path).ShouldBe(Error.Ok);
            store.Load().Language.ShouldBe("uk");
            Should.Throw<ArgumentException>(() => new PlayerPreferences(false, true, 0.8f, language: "unknown"));
            TranslationServer.SetLocale("en");
            var failing = new PreferencesStore($"user://missing-{Guid.NewGuid():N}/settings.cfg");
            failing.SaveAndApply(PlayerPreferences.Default).ShouldNotBe(Error.Ok);
            TranslationServer.GetLocale().ShouldBe("en");
        }
        finally
        {
            DirAccess.RemoveAbsolute(ProjectSettings.GlobalizePath(path));
            TranslationServer.SetLocale(previous);
        }
    }

    /// <summary>Live switching retains equipment, captured affix values, independent pins and searchable reference identity.</summary>
    /// <returns>Completes after both locales render against the same live session.</returns>
    [Test]
    public async Task LiveLanguageChangesRetainSessionAndPinnedFacts()
    {
        var previous = TranslationServer.GetLocale();
        TranslationServer.SetLocale("en");
        var runtime = TestScene.GetTree().Root.GetChildren()
            .OfType<GameBootstrap>()
            .Single();
        var arena = GD.Load<PackedScene>("res://Arena.tscn").Instantiate<GreyboxArena>();
        runtime.AddChild(arena);
        ArenaItemFixtures.UseAuthoredKit(arena);
        arena.Player.Controls.HardwareInput = false;
        arena.Opponent.Controls.ThinkingEnabled = false;
        var reference = new ReferencePanel();
        TestScene.AddChild(reference);

        try
        {
            var hud = arena.GetNode<ArenaHud>("Presentation");
            var owner = arena.Player.CombatantId;
            var state = runtime.CombatState.ReadEquipment(owner);
            var bow = state.OwnedItems.Values.Single(item => item.Base.Id == "ashen-longbow");
            new EquipmentService(runtime.CombatState).EquipItem(owner, bow.Id, EquipmentSlot.Weapon);
            hud.ToggleInventory();
            hud.ToggleCharacter();
            var stack = hud.GetNode<InspectionStack>("Root/Design/Inspections");
            var captured = ItemInspectionTopics.Affix(bow.Rarity, bow.Affixes[0], true);
            stack.Open(captured, new Vector2(500, 100));
            var pane = stack.GetNode<InspectionPane>("Level0");
            pane.GetNode<Button>("Card/Pin").EmitSignal(BaseButton.SignalName.Pressed);
            await Frames(2);
            var position = pane.Position;
            var before = runtime.CombatState.ReadEquipment(owner);
            var catalog = ReferenceCatalog.Create(before.OwnedItems.Values);
            var entry = catalog.Single(value => value.Id == $"item.{bow.Id}");
            reference.Open(catalog);
            reference.SelectSection("rules");
            var category = reference.GetNode<OptionButton>("Book/Category");
            var statsIndex = Enumerable.Range(0, category.ItemCount)
                .Single(index => category.GetItemText(index) == "Character stats");
            category.Select(statsIndex);
            category.EmitSignal(OptionButton.SignalName.ItemSelected, (long)statsIndex);
            var search = reference.GetNode<LineEdit>("Book/Search");
            search.Text = "armor";
            search.EmitSignal(LineEdit.SignalName.TextChanged, search.Text);
            TranslationServer.SetLocale("uk");
            await Frames(2);
            pane.GetNode<Label>("Card/Title").Text.ShouldContain("Тверда рука");
            var body = pane.GetNode<RichTextLabel>("Card/Body").GetParsedText();
            body.ShouldContain("72%");
            body.ShouldContain("Доступні ранги");
            pane.IsPinned.ShouldBeTrue();
            pane.Position.ShouldBe(position);
            runtime.CombatState.ReadEquipment(owner).Version.ShouldBe(before.Version);
            runtime.CombatState.ReadEquipment(owner).Loadout.GetItem(EquipmentSlot.Weapon).Id.ShouldBe(bow.Id);
            entry.Matches("попелястий лук").ShouldBeTrue();
            entry.Topic.Title.ShouldBe("Попелястий довгий лук");
            category.GetItemText(category.Selected).ShouldBe("Характеристики");
            search.Text.ShouldBe("armor");
            reference.GetNode<RichTextLabel>("Book/Article").GetParsedText().ShouldContain(CombatGlossary.Armor().Description);
            TranslationServer.SetLocale("en");
            await Frames(2);
            pane.GetNode<Label>("Card/Title").Text.ShouldContain("steady hand");
            pane.GetNode<RichTextLabel>("Card/Body").GetParsedText().ShouldContain("72%");
            entry.Topic.Title.ShouldBe("Ashen Longbow");
            runtime.CombatState.ReadEquipment(owner).Version.ShouldBe(before.Version);
            runtime.CombatState.ReadEquipment(owner).Loadout.GetItem(EquipmentSlot.Weapon).Id.ShouldBe(bow.Id);
        }
        finally
        {
            reference.Free();
            arena.Free();
            TranslationServer.SetLocale(previous);
        }
    }

    private async Task Frames(int count)
    {
        for (var index = 0; index < count; index++)
        {
            await TestScene.ToSignal(TestScene.GetTree(), SceneTree.SignalName.ProcessFrame);
        }
    }
}
