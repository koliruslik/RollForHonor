using System;
using Godot;
using RollForHonor.Godot.Arena;
using RollForHonor.Domain.Items.Models;
using RollForHonor.Godot.UI.Common;
using RollForHonor.Godot.UI.Inventory;
using RollForHonor.Godot.UI.Localization;

namespace RollForHonor.Godot.UI.Hud;

internal sealed class ArenaRoundPresenter
{
    internal string SelectedLootId => _nearbyLoot.Visible ? _nearbyLoot.SelectedItemId : null;

    private readonly Control _design;
    private readonly Func<bool> _isBlocked;
    private readonly Func<float> _occupiedWidth;
    private readonly Action<ItemInstance> _hoverItem;
    private GameBootstrap _runtime;
    private GreyboxArena _arena;
    private NearbyLootPanel _nearbyLoot;
    private Panel _roundBanner;
    private Label _roundTitle;
    private Label _lootPrompt;
    private Button _nextRound;
    private Panel _capacityNotice;
    private TimeSpan _capacityNoticeUntil;
    private int _dismissedRound = -1;

    internal ArenaRoundPresenter(Control design, Func<bool> isBlocked, Func<float> occupiedWidth, Action<ItemInstance> hoverItem)
    {
        _design = design;
        _isBlocked = isBlocked;
        _occupiedWidth = occupiedWidth;
        _hoverItem = hoverItem;
        Build();
    }

    internal void Configure(GameBootstrap runtime, GreyboxArena arena)
    {
        _runtime = runtime;
        _arena = arena;
    }

    internal void ShowCapacityWarning()
    {
        _capacityNoticeUntil = _runtime.GameTime + TimeSpan.FromSeconds(5);
        _capacityNotice.Show();
        _capacityNotice.MoveToFront();
    }

    internal void Refresh(bool alive)
    {
        _roundBanner.Position = new Vector2((_design.Size.X - 620) / 2, 138);
        _nextRound.Position = new Vector2(_design.Size.X - 370, 372);
        var available = _arena.RoundsEnabled && alive && !_isBlocked();
        var nearbyDrops = _arena.NearbyDrops();
        var nearbyGold = _arena.NearbyGoldDrops();
        _nearbyLoot.Refresh(nearbyDrops, nearbyGold, _arena.AutoGoldPickupEnabled);
        _nearbyLoot.Position = new Vector2((_design.Size.X - _occupiedWidth() - 320) / 2, 840 - _nearbyLoot.Size.Y);
        _lootPrompt.Position = new Vector2((_design.Size.X - 420) / 2, 850);
        _nearbyLoot.Visible = available && (nearbyDrops.Count > 0 || nearbyGold.Count > 0);
        _capacityNotice.Position = new Vector2((_design.Size.X - 560) / 2, 330);
        _capacityNotice.Visible = available && _runtime.GameTime < _capacityNoticeUntil;
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
            L.SetText(_design.GetNode<Label>("Objective/ObjectiveText"), _arena.RoundComplete ? L.Text("Free time")
                : L.Format($"Round {_arena.RoundNumber} · Defeated {_arena.DefeatedCount}/{GreyboxArena.EnemiesPerRound}"));
        }
    }

    private void Build()
    {
        _capacityNotice = UiLayout.Panel(_design, "InventoryFullNotice", new Rect2(680, 330, 560, 94));
        _capacityNotice.MouseFilter = Control.MouseFilterEnum.Ignore;
        _capacityNotice.AddThemeStyleboxOverride("panel", HonorTheme.Panel(new Color("38201f"), new Color("d28d75"), 12));
        var warning = UiLayout.Place(_capacityNotice, UiLayout.Text(L.Text("Inventory full! Make room to pick up this item."), 23, new Color("ffe4d5")), new Rect2(20, 10, 520, 74));
        warning.HorizontalAlignment = HorizontalAlignment.Center;
        warning.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        _capacityNotice.Hide();
        _nearbyLoot = UiLayout.Place(_design, new NearbyLootPanel { Name = "NearbyLoot" }, new Rect2(750, 530, 420, 320));
        _nearbyLoot.IconKey = item => ItemPresentation.Icon(item, ItemPresentation.Slot(item, _runtime.CombatState.ReadEquipment(_arena.Player.CombatantId).Rules));
        _nearbyLoot.AutoGoldToggleRequested += () => _arena.ToggleAutoGoldPickup();
        _nearbyLoot.PickupRequested += id => _arena.PickUpLoot(id);
        _nearbyLoot.ItemHovered += _hoverItem;
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
}
