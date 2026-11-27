using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Chickensoft.GoDotTest;
using Godot;
using RollForHonor.Domain.Combat.State.Models;
using RollForHonor.Domain.Dice.Models;
using RollForHonor.Domain.Dice.Services;
using RollForHonor.Domain.Items.Models;
using RollForHonor.Godot.Content.Loot;
using RollForHonor.Godot.Arena;
using RollForHonor.Godot.UI.Hud;
using RollForHonor.Godot.UI.Reference;
using RollForHonor.Godot.UI.Inventory;
using RollForHonor.Infrastructure.Dice;
using Shouldly;

namespace RollForHonor.Godot.Tests;

/// <summary>Verifies live content gates, exact rarity boundaries and native reference coverage.</summary>
/// <param name="testScene">The active engine test host.</param>
public sealed class GodotLootCatalogTest(Node testScene) : TestClass(testScene)
{
    /// <summary>Every accepted base supports the full rarity capacity using real enabled and level-gated families.</summary>
    [Test]
    public void EveryBaseGeneratesWithoutInertAffixesOrCapacityFailures()
    {
        GameLootCatalog.Bases.Count.ShouldBe(203);
        GameLootCatalog.Affixes.Count.ShouldBe(57);
        GameLootCatalog.Affixes
            .SelectMany(family => family.GetProperty("tiers").EnumerateObject())
            .All(tier => tier.Value.GetProperty("tier_id").GetString() == $"T{8 - int.Parse(tier.Name)}")
            .ShouldBeTrue();
        GameLootCatalog.Affixes.Sum(family => family.GetProperty("tiers").EnumerateObject().Count()).ShouldBe(364);

        foreach (var entry in GameLootCatalog.Bases.Where(entry => !entry.TryGetProperty("released", out var released) || released.GetBoolean()))
        {
            var item = GameLootCatalog.CreateItem(entry.GetProperty("id").GetString(), Guid.NewGuid().ToString(), ItemRarity.Legendary,
                new RandomDiceRoller(new Random(624)), occupancyPercent: 100);
            item.Affixes.Count.ShouldBe(8);
            item.Affixes
                .Select(affix => affix.FamilyId)
                .Distinct()
                .Count()
                .ShouldBe(8);
            item.Affixes.All(affix => affix.RollContext is not null).ShouldBeTrue();
            item.Affixes.All(affix => affix.DefinitionId is not ("life-leech" or "mana-leech" or "critical-bonus" or "critical-multiplier")).ShouldBeTrue();
            item.Affixes.All(affix => affix.TierId != "T0").ShouldBeTrue();
        }
    }

    /// <summary>Live data composes the approved defence totals and rejects unbalanced future bases.</summary>
    [Test]
    public void FirstGenerationDefensesPreserveFractionalContributions()
    {
        foreach (var sample in new[] { (Branch: "strength", Armor: 130m, Evasion: 0m), (Branch: "dexterity", Armor: 0m, Evasion: 100m), (Branch: "intelligence", Armor: 45m, Evasion: 45m) })
        {
            var modifiers = new[] { "body", "helmet", "boots" }
                .Select(slot => GameLootCatalog.CreateItem($"{sample.Branch}-{slot}-travel", Guid.NewGuid().ToString(), ItemRarity.Common, new PrefixDice([])))
                .SelectMany(item => new RollForHonor.Domain.Items.Services.ItemModifierProjector().Project(item))
                .ToArray();
            var stats = new RollForHonor.Domain.Stats.StatCalculator().Calculate(new(new(), modifiers));
            stats.Get(new(RollForHonor.Domain.Stats.StatType.Armor)).ShouldBe(sample.Armor);
            stats.Get(new(RollForHonor.Domain.Stats.StatType.Evasion)).ShouldBe(sample.Evasion);
        }

        Should.Throw<InvalidOperationException>(() => GameLootCatalog.CreateItem("strength-body-field", "pending", ItemRarity.Common, new PrefixDice([])));
    }

    /// <summary>Each starting branch grants its selected weapon with the matching inventory and attack icon.</summary>
    /// <returns>Completes after optional native weapon screenshots are captured.</returns>
    [Test]
    public async Task EachStarterChoiceIsUsableWithoutAttributePoints()
    {
        var runtime = TestScene.GetTree().Root.GetChildren()
            .OfType<GameBootstrap>()
            .Single();

        foreach (var branch in new[] { "dexterity", "strength", "intelligence" })
        {
            var arena = GD.Load<PackedScene>("res://Arena/RoundArena.tscn").Instantiate<GreyboxArena>();
            runtime.AddChild(arena);
            var locale = TranslationServer.GetLocale();

            try
            {
                if (branch == "dexterity")
                {
                    foreach (var language in new[] { "uk", "en" })
                    {
                        TranslationServer.SetLocale(language);

                        for (var frame = 0; frame < 3; frame++)
                        {
                            await TestScene.ToSignal(TestScene.GetTree(), SceneTree.SignalName.ProcessFrame);
                        }

                        var panel = arena.GetNode<ArenaHud>("Presentation").GetNode<Panel>("Root/Design/StarterSelection/Choice");
                        var description = panel.GetNode<Label>("Description");
                        description.GetRect().End.X.ShouldBeLessThanOrEqualTo(panel.Size.X - 30);
                        description.GetRect().End.Y.ShouldBeLessThanOrEqualTo(240);
                        description.GetLineCount().ShouldBeGreaterThan(1);
                        description.GetMinimumSize().Y.ShouldBeLessThanOrEqualTo(description.Size.Y);
                        var starterCapture = OS.GetEnvironment("RFH_STARTER_CAPTURE_DIRECTORY");

                        if (!string.IsNullOrEmpty(starterCapture))
                        {
                            await TestScene.ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
                            TestScene.GetViewport().GetTexture().GetImage()
                                .SavePng(System.IO.Path.Combine(starterCapture, $"starter-{language}.png")).ShouldBe(Error.Ok);
                        }
                    }

                    TranslationServer.SetLocale(locale);
                }

                arena.SelectStarterWeapon($"{branch}-weapon-travel").ShouldBeTrue();
                arena.NeedsStarterWeapon.ShouldBeFalse();
                TestScene.GetTree().Paused.ShouldBeFalse();
                var equipment = runtime.CombatState.ReadEquipment(arena.Player.CombatantId);
                equipment.OwnedItems.Count.ShouldBe(1);
                var weapon = equipment.Loadout.GetItem(RollForHonor.Domain.Items.Equipment.Models.EquipmentSlot.Weapon);
                weapon.Base.Id.ShouldBe($"{branch}-weapon-travel");
                weapon.Base.Weapon.AttacksPerSecond.ShouldBe(4);
                weapon.Base.Weapon.ManaCost.ShouldBe(10);
                var icon = branch switch { "strength" => "sword", "intelligence" => "wand", _ => "weapon" };
                ItemPresentation.Icon(weapon, RollForHonor.Domain.Items.Equipment.Models.EquipmentSlot.Weapon).ShouldBe(icon);
                GD.Load<Texture2D>($"res://UI/Assets/{icon}.svg").GetWidth().ShouldBe(96);
                var hud = arena.GetNode<ArenaHud>("Presentation");
                hud._Process(0);
                hud.GetNode<TextureRect>("Root/Design/ActionBar/PrimaryAttack/Icon").Texture.ResourcePath.ShouldBe($"res://UI/Assets/{icon}.svg");
                runtime.CombatState.GetStats(arena.Player.CombatantId).Get(new(RollForHonor.Domain.Stats.StatType.Strength)).ShouldBe(0);
                arena.SelectStarterWeapon($"{branch}-weapon-travel").ShouldBeFalse();
                var capture = OS.GetEnvironment("RFH_WEAPON_CAPTURE_DIRECTORY");

                if (!string.IsNullOrWhiteSpace(capture))
                {
                    arena.Player.Controls.HardwareInput = false;
                    hud.ToggleInventory();
                    await TestScene.ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
                    TestScene.GetViewport().GetTexture().GetImage().SavePng(System.IO.Path.Combine(capture, $"{branch}.png")).ShouldBe(Error.Ok);
                }
            }
            finally
            {
                TranslationServer.SetLocale(locale);
                arena.Free();
            }
        }
    }

    /// <summary>The single categorical equipment roll reproduces every exact per-kill rarity boundary.</summary>
    [Test]
    public void SentinelRarityBoundariesKeepGoldIndependent()
    {
        var cases = new (int Roll, ItemRarity? Rarity)[]
        {
            (1, null), (550000, null), (550001, ItemRarity.Common), (800000, ItemRarity.Common),
            (800001, ItemRarity.Uncommon), (950000, ItemRarity.Uncommon), (950001, ItemRarity.Rare),
            (990000, ItemRarity.Rare), (990001, ItemRarity.Epic), (999000, ItemRarity.Epic), (999001, ItemRarity.Legendary), (1000000, ItemRarity.Legendary)
        };

        foreach (var sample in cases)
        {
            var result = GameLootCatalog.Roll("sentinel", new(Guid.NewGuid()), new PrefixDice([1, 100, sample.Roll]), 0);
            result.Gold.GoldAmount.ShouldBe(14);
            result.Equipment.SelectionRoll.Total.ShouldBe(sample.Roll);
            ((ItemRarity?)result.Item?.Rarity).ShouldBe(sample.Rarity);

            if (result.Item is not null)
            {
                result.Item.Affixes.All(affix => affix.TierId == "T7").ShouldBeTrue();
            }
        }
    }

    /// <summary>At a shared guaranteed-top-rarity face, Luck still preserves slot/base weights and affix gates.</summary>
    [Test]
    public void CapturedLuckPreservesWeightsAndItemLevelGates()
    {
        var source = new CombatantId(Guid.NewGuid());
        var ordinary = GameLootCatalog.Roll("sentinel", source, new PrefixDice([1, 1, 1000000]), 0);
        var lucky = GameLootCatalog.Roll("sentinel", source, new PrefixDice([1, 1, 1000000]), 100);
        ordinary.Gold.GoldAmount.ShouldBe(6);
        lucky.Gold.GoldAmount.ShouldBe(14);
        lucky.Item.Base.Id.ShouldBe(ordinary.Item.Base.Id);
        lucky.Item.Rarity.ShouldBe(ordinary.Item.Rarity);
        lucky.Item.Affixes.Count.ShouldBe(ordinary.Item.Affixes.Count);
        lucky.Item.Affixes.Count.ShouldBeGreaterThan(0);
        lucky.Item.Affixes.All(affix => affix.TierId == "T7" && affix.EffectiveQualityRoll == 100).ShouldBeTrue();
        lucky.Item.Affixes.All(affix => affix.RollContext.EffectiveTierRoll == affix.RollContext.TierRoll.Total + 5).ShouldBeTrue();
    }

    /// <summary>The real reward path and displayed probabilities use the same captured player Luck.</summary>
    /// <returns>Completes after locale switching and an optional rendered probability-table capture.</returns>
    [Test]
    public async Task LuckChangesOccurrenceAndRarityWithoutRerolling()
    {
        var source = new CombatantId(Guid.NewGuid());
        var naturalDice = new PrefixDice([1, 1, 500000]);
        var luckyDice = new PrefixDice([1, 1, 500000]);
        var natural = GameLootCatalog.Roll("sentinel", source, naturalDice, 0);
        var lucky = GameLootCatalog.Roll("sentinel", source, luckyDice, 100);
        natural.Item.ShouldBeNull();
        lucky.Item.Rarity.ShouldBe(ItemRarity.Common);
        lucky.Equipment.SelectionRoll.Total.ShouldBe(500000);
        lucky.Equipment.Luck.Value.ShouldBe(100);
        lucky.Equipment.SelectionRoll.Formula.Sides.ShouldBe(1000000);
        luckyDice.Trace.Rolls[2].ShouldBeSameAs(lucky.Equipment.SelectionRoll);
        var epic = GameLootCatalog.Roll("sentinel", source, new PrefixDice([1, 1, 990000]), 100);
        epic.Item.Rarity.ShouldBe(ItemRarity.Epic);
        var table = LootReference.Create(100).Single(entry => entry.Id == "loot.monster.sentinel");
        table.CapturedLuck.ShouldBe(100);
        table.Topic.Description.ShouldContain("69.75");
        var commonChance = decimal.Parse(table.Table.Rows.Single(row => row.Id == "Common.dexterity-weapon-travel").Cells[2].TrimEnd('%'), System.Globalization.CultureInfo.InvariantCulture);
        commonChance.ShouldBe(2.8125m);
        var runtime = TestScene.GetTree().Root.GetChildren()
            .OfType<GameBootstrap>()
            .Single();
        var arena = GD.Load<PackedScene>("res://Arena/RoundArena.tscn").Instantiate<GreyboxArena>();
        runtime.AddChild(arena);
        arena.SelectStarterWeapon("dexterity-weapon-travel").ShouldBeTrue();
        arena.Player.Controls.HardwareInput = false;
        var previousLocale = TranslationServer.GetLocale();

        try
        {
            var profile = runtime.CombatState.ReadEquipment(arena.Player.CombatantId).NonEquipmentProfile;
            runtime.CombatState.UpdateNonEquipmentStats(arena.Player.CombatantId,
                new(profile.BaseValues.With(new(RollForHonor.Domain.Stats.StatType.Luck), 100), profile.Modifiers));
            var hud = arena.GetNode<ArenaHud>("Presentation");
            hud.ToggleReference();
            var panel = hud.GetNode<ReferencePanel>("Root/Design/Reference");
            panel.GetNode<RichTextLabel>("Book/Article").GetParsedText().ShouldContain("69.75");
            TranslationServer.SetLocale("uk");
            await TestScene.ToSignal(TestScene.GetTree(), SceneTree.SignalName.ProcessFrame);
            panel.GetNode<RichTextLabel>("Book/Article").GetParsedText().ShouldContain("69.75");
            var root = OS.GetEnvironment("RFH_LUCK_CAPTURE_DIRECTORY");

            if (!string.IsNullOrWhiteSpace(root))
            {
                await TestScene.ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
                TestScene.GetViewport().GetTexture().GetImage().SavePng(System.IO.Path.Combine(root, "luck-100.png")).ShouldBe(Error.Ok);
            }
        }
        finally
        {
            arena.Free();
            TranslationServer.SetLocale(previousLocale);
        }
    }

    /// <summary>Reference entries cover the same live families, bases and monster records without creating items.</summary>
    [Test]
    public void ReferenceIncludesEveryTableAndSelectableAffixTier()
    {
        var entries = LootReference.Create();
        entries.Single(entry => entry.Id == "loot.bases").Table.Rows.Count.ShouldBe(525);
        var affixes = entries.Single(entry => entry.Id == "loot.affixes").Table;
        affixes.Rows.Count.ShouldBe(57);
        affixes.Tiers.Count.ShouldBe(7);
        affixes.Columns.Any(column => column.Title == "Status").ShouldBeFalse();
        affixes.Rows.Single(row => row.Id == "dice-fire").Cells[2].ShouldContain("1d4");
        var loot = entries.Single(entry => entry.Id == "loot.monster.sentinel").Table;
        loot.Rows.Single(row => row.Id == "gold").Cells[3].ShouldBe("6–14");
        loot.Rows.Count(row => row.FilterTags.StartsWith("Legendary|", StringComparison.Ordinal)).ShouldBe(21);

    }

    /// <summary>The actual arena starts with current catalog items and exposes the live monster table in native controls.</summary>
    /// <returns>Completes after native controls render; optional captures require a rendered test run.</returns>
    [Test]
    public async Task ArenaUsesCurrentKitAndReferenceWithoutLegacyDropContent()
    {
        var runtime = TestScene.GetTree().Root.GetChildren()
            .OfType<GameBootstrap>()
            .Single();
        var arena = GD.Load<PackedScene>("res://Arena/RoundArena.tscn").Instantiate<GreyboxArena>();
        runtime.AddChild(arena);
        arena.Player.Controls.HardwareInput = false;
        arena.Opponent.Controls.ThinkingEnabled = false;
        var previousLocale = TranslationServer.GetLocale();

        try
        {
            runtime.CombatState.ReadEquipment(arena.Player.CombatantId).OwnedItems.Count.ShouldBe(0);
            arena.NeedsStarterWeapon.ShouldBeTrue();
            await Capture("starter-choice");
            arena.SelectStarterWeapon("dexterity-weapon-travel").ShouldBeTrue();
            arena.SelectStarterWeapon("strength-weapon-travel").ShouldBeFalse();
            var items = runtime.CombatState.ReadEquipment(arena.Player.CombatantId).OwnedItems.Values;
            items.Count().ShouldBe(1);
            items.Single().Base.Name.ShouldBe("Wooden Bow");
            items.Single().Rarity.ShouldBe(ItemRarity.Common);
            var hud = arena.GetNode<ArenaHud>("Presentation");
            hud.ToggleInventory();

            await Capture("inventory");
            hud.ToggleReference();
            var reference = hud.GetNode<ReferencePanel>("Root/Design/Reference");
            var button = reference.FindChildren("*", "Button", true, false)
                .OfType<Button>()
                .Single(value => value.HasMeta("reference_id") && value.GetMeta("reference_id").AsString() == "loot.monster.sentinel");
            button.EmitSignal(BaseButton.SignalName.Pressed);
            reference.GetNode<ReferenceTableView>("Book/Table").VisibleRowCount.ShouldBe(106);
            await Capture("sentinel-table");
            var rarityFilter = reference.GetNode<OptionButton>("Book/Category");
            var rare = Enumerable.Range(0, rarityFilter.ItemCount)
                .Single(index => rarityFilter.GetItemText(index) == RollForHonor.Godot.UI.Localization.L.Text("Rare"));
            rarityFilter.Select(rare);
            rarityFilter.EmitSignal(OptionButton.SignalName.ItemSelected, rare);
            reference.GetNode<ReferenceTableView>("Book/Table").VisibleRowCount.ShouldBe(21);
            await Capture("sentinel-rare");
            reference.SelectSection("affixes");
            reference.GetNode<ReferenceTableView>("Book/Table").VisibleRowCount.ShouldBe(57);
            await Capture("affixes");
            var tier = reference.GetNode<OptionButton>("Book/Tier");
            tier.Select(2);
            tier.EmitSignal(OptionButton.SignalName.ItemSelected, 2);
            await Capture("affixes-t5");
            reference.SelectSection("items");
            reference.GetNode<ReferenceTableView>("Book/Table").VisibleRowCount.ShouldBe(525);
            await Capture("bases");
            TranslationServer.SetLocale("uk");
            reference.SelectSection("items");
            await Capture("items-uk");
            reference.SelectSection("affixes");
            reference.GetNode<ReferenceTableView>("Book/Table").VisibleRowCount.ShouldBe(57);
            reference.GetNode<OptionButton>("Book/Tier").GetItemText(0).ShouldContain("Тір");
            await Capture("affixes-uk");
            reference.SelectSection("monsters");
            await Capture("sentinel-uk");
        }
        finally
        {
            TranslationServer.SetLocale(previousLocale);
            arena.Free();
        }
    }

    private async Task Capture(string name)
    {
        var root = OS.GetEnvironment("RFH_LOOT_CAPTURE_DIRECTORY");

        if (string.IsNullOrWhiteSpace(root))
        {
            return;
        }

        await TestScene.ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
        TestScene.GetViewport().GetTexture().GetImage().SavePng(System.IO.Path.Combine(root, $"{name}.png")).ShouldBe(Error.Ok);
    }

    private sealed class PrefixDice(IEnumerable<int> values) : IDiceRoller
    {
        /// <inheritdoc />
        public DiceTrace Trace => new(_rolls);

        private readonly Queue<int> _values = new(values);
        private readonly List<DiceRoll> _rolls = [];
        private readonly RandomDiceRoller _fallback = new(new Random(951));

        /// <inheritdoc />
        public DiceRoll Roll(DiceFormula formula)
        {
            DiceRoll roll;

            if (_values.Count > 0)
            {
                var value = _values.Dequeue();
                roll = new(formula, [value], value);
            }
            else
            {
                roll = _fallback.Roll(formula);
            }

            _rolls.Add(roll);

            return roll;
        }
    }
}
