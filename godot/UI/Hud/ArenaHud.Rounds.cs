using System.Linq;
using Godot;
using RollForHonor.Domain.Items.Inventory.Models;
using RollForHonor.Godot.UI.Inventory;
using RollForHonor.Application.Items;
using RollForHonor.Domain.Items.Models;
using RollForHonor.Godot.Arena;
using RollForHonor.Godot.Gameplay.Input;
using RollForHonor.Godot.UI.Common;
using RollForHonor.Godot.UI.Localization;

namespace RollForHonor.Godot.UI.Hud;

public partial class ArenaHud
{
    /// <summary>Gets the selected nearby-menu item when that menu is available.</summary>
    public string SelectedLootId => _nearbyLoot.Visible ? _nearbyLoot.SelectedItemId : null;

    private NearbyLootPanel _nearbyLoot;
    private Panel _roundBanner;
    private Label _roundTitle;
    private Label _lootPrompt;
    private Button _nextRound;
    private int _dismissedRound = -1;

    /// <summary>Capacity-checks and publishes one captured pickup against the live bag layout.</summary>
    /// <param name="item">The unowned dropped instance.</param>
    /// <param name="destination">Optional exact top-left bag cell.</param>
    /// <returns>The acquisition result; rejection leaves the item in the world.</returns>
    public ItemAcquisitionStatus AcquireItem(ItemInstance item, InventoryPosition? destination = null)
    {
        var state = _runtime.CombatState;
        _inventory.Refresh(state.ReadEquipment(_arena.Player.CombatantId));
        var result = new ItemAcquisitionService(state).Acquire(_arena.Player.CombatantId, item, _inventory.CurrentLayout, destination);

        if (result == ItemAcquisitionStatus.Acquired)
        {
            RefreshWindows();
        }

        return result;
    }

    private void BuildRoundUi()
    {
        _nearbyLoot = UiLayout.Place(_design, new NearbyLootPanel { Name = "NearbyLoot" }, new Rect2(750, 530, 420, 320));
        _nearbyLoot.IconKey = item => ItemPresentation.Icon(item, ItemPresentation.Slot(item, _runtime.CombatState.ReadEquipment(_arena.Player.CombatantId).Rules));
        _nearbyLoot.PickupRequested += id => _arena.PickUpItem(id);
        _nearbyLoot.ItemHovered += HoverItem;
        _roundBanner = UiLayout.Panel(_design, "RoundComplete", new Rect2(650, 138, 620, 172));
        _roundTitle = UiLayout.Place(_roundBanner, UiLayout.Text("", 25, HonorTheme.Brass, true), new Rect2(24, 16, 530, 40));
        UiLayout.Place(_roundBanner, UiLayout.Text(L.Text("Free time — explore and collect your loot."), 18), new Rect2(24, 62, 572, 36));
        UiLayout.Place(_roundBanner, UiLayout.Button(L.Text("Continue exploring"), () => _dismissedRound = _arena.RoundNumber), new Rect2(24, 110, 270, 44));
        UiLayout.Place(_roundBanner, UiLayout.Button(L.Text("Next round"), () => _arena.StartNextRound(), true), new Rect2(310, 110, 286, 44));
        _roundBanner.Hide();
        _nextRound = UiLayout.Place(_design, UiLayout.Button(L.Text("Next round"), () => _arena.StartNextRound()), new Rect2(1550, 372, 333, 48));
        _nextRound.Name = "NextRound";
        _nextRound.Hide();
        _lootPrompt = UiLayout.Place(_design, UiLayout.Text("", 18, HonorTheme.Brass), new Rect2(750, 858, 420, 64));
        _lootPrompt.Name = "LootPrompt";
        _lootPrompt.HorizontalAlignment = HorizontalAlignment.Center;
        _lootPrompt.AutowrapMode = TextServer.AutowrapMode.WordSmart;
    }

    private void RefreshRoundUi(bool alive)
    {
        _roundBanner.Position = new Vector2((_design.Size.X - 620) / 2, 138);
        _nextRound.Position = new Vector2(_design.Size.X - 370, 372);
        var available = _arena.RoundsEnabled && alive && !_pause.Visible && !_settings.Visible && !_reference.Visible;
        var nearbyDrops = _arena.NearbyDrops();
        _nearbyLoot.Refresh(nearbyDrops);
        _nearbyLoot.Position = new Vector2((_design.Size.X - (_inventory.Visible ? 560 : 0) + (_character.Visible ? 360 : 0) - 420) / 2, 530);
        _lootPrompt.Position = _nearbyLoot.Position + new Vector2(0, 328);
        _nearbyLoot.Visible = available && nearbyDrops.Count > 0;
        _roundBanner.Visible = available && _arena.RoundComplete && _dismissedRound != _arena.RoundNumber;
        _nextRound.Visible = available && _arena.RoundComplete;
        _lootPrompt.Visible = available;

        if (!_arena.RoundsEnabled)
        {
            return;
        }

        L.SetText(_roundTitle, L.Format($"Round {_arena.RoundNumber} complete"));
        L.SetText(_lootPrompt, _arena.PickupFeedback);

        if (alive)
        {
            L.SetText(_objective, _arena.RoundComplete ? L.Text("Free time")
                : L.Format($"Round {_arena.RoundNumber} · Defeated {_arena.DefeatedCount}/{GreyboxArena.EnemiesPerRound}"));
        }
    }
}
