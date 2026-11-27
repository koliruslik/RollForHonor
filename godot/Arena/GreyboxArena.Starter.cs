using System;
using System.Linq;
using Godot;
using RollForHonor.Domain.Items.Models;
using RollForHonor.Godot.Content.Loot;
using RollForHonor.Godot.UI.Common;
using RollForHonor.Godot.UI.Hud;
using RollForHonor.Godot.UI.Localization;
using RollForHonor.Infrastructure.Dice;

namespace RollForHonor.Godot.Arena;

public partial class GreyboxArena
{
    /// <summary>Gets whether a playable run is waiting for its one starting-weapon choice.</summary>
    public bool NeedsStarterWeapon => RoundsEnabled && !_starterSelected;

    private bool _starterSelected;
    private bool _starterPreviousPause;
    private Control _starterOverlay;

    /// <summary>Chooses one of the released first-generation weapons, equipping it without granting points or other items.</summary>
    /// <param name="baseId">The first-generation bow, sword or wand base identity.</param>
    /// <returns>True for the first accepted choice, false for invalid or repeated choices.</returns>
    public bool SelectStarterWeapon(string baseId)
    {
        if (!NeedsStarterWeapon || !GameLootCatalog.Bases.Any(entry => entry.GetProperty("id").GetString() == baseId
            && entry.TryGetProperty("generation", out var generation) && generation.GetInt32() == 1
            && entry.GetProperty("slot").GetString() == "weapon"))
        {
            return false;
        }

        var item = GameLootCatalog.CreateItem(baseId, $"{Player.CombatantId.Value:N}:starter", ItemRarity.Common, new RandomDiceRoller(new Random()));

        if (!_runtime.CombatState.TrySelectStarterWeapon(Player.CombatantId, item))
        {
            return false;
        }

        _starterSelected = true;
        _starterOverlay.Hide();
        GetTree().Paused = _starterPreviousPause;

        return true;
    }

    /// <inheritdoc />
    public override void _ExitTree()
    {
        if (NeedsStarterWeapon && _starterOverlay is not null)
        {
            GetTree().Paused = _starterPreviousPause;
        }
    }

    private void ShowStarterSelection()
    {
        if (!RoundsEnabled)
        {
            return;
        }

        var design = GetNode<ArenaHud>("Presentation").GetNode<Control>("Root/Design");
        _starterOverlay = UiLayout.Place(design, new Control { Name = "StarterSelection", ProcessMode = ProcessModeEnum.Always }, new Rect2(0, 0, design.Size.X, 1080));
        UiLayout.Place(_starterOverlay, new ColorRect { Color = new Color(0, 0, 0, 0.85f) }, new Rect2(0, 0, design.Size.X, 1080));
        var panel = UiLayout.Panel(_starterOverlay, "Choice", new Rect2((design.Size.X - 1000) / 2, 250, 1000, 500));
        UiLayout.Place(panel, UiLayout.Text(L.Text("Choose your starting weapon"), 30, HonorTheme.Brass, true), new Rect2(30, 24, 940, 54));
        var note = UiLayout.Text(L.Text("Choose one Common weapon. No class is locked. For now, all three fire the same arrow and use the same mana and cooldown."), 20);
        note.Name = "Description";
        note.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        UiLayout.Place(panel, note, new Rect2(30, 96, 940, 110));
        var choices = GameLootCatalog.Bases.Where(entry => entry.TryGetProperty("generation", out var generation)
            && generation.GetInt32() == 1 && entry.GetProperty("slot").GetString() == "weapon").ToArray();

        for (var index = 0; index < choices.Length; index++)
        {
            var entry = choices[index];
            var id = entry.GetProperty("id").GetString();
            var button = UiLayout.Place(panel, UiLayout.Button(L.Text(entry.GetProperty("name").GetString()), () => SelectStarterWeapon(id)), new Rect2(30 + 320 * index, 240, 300, 110));
            button.Name = id;
            button.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        }

        _starterPreviousPause = GetTree().Paused;
        GetTree().Paused = true;
    }
}
