using Godot;
using RollForHonor.Application.Items;
using RollForHonor.Domain.Items.Models;
using RollForHonor.Godot.Arena;
using RollForHonor.Godot.Gameplay.Input;
using RollForHonor.Godot.UI.Common;
using RollForHonor.Godot.UI.Localization;

namespace RollForHonor.Godot.UI.Hud;

public partial class ArenaHud
{
    private Panel _roundBanner;
    private Label _roundTitle;
    private Label _lootPrompt;
    private Button _nextRound;
    private int _dismissedRound = -1;

    /// <summary>Capacity-checks and publishes one captured pickup against the live bag layout.</summary>
    /// <param name="item">The unowned dropped instance.</param>
    /// <returns>The acquisition result; rejection leaves the item in the world.</returns>
    public ItemAcquisitionStatus AcquireItem(ItemInstance item)
    {
        var state = _runtime.CombatState;
        _inventory.Refresh(state.ReadEquipment(_arena.Player.CombatantId));
        var result = new ItemAcquisitionService(state).Acquire(_arena.Player.CombatantId, item, _inventory.CurrentLayout);

        if (result == ItemAcquisitionStatus.Acquired)
        {
            RefreshWindows();
        }

        return result;
    }

    private void BuildRoundUi()
    {
        _roundBanner = UiLayout.Panel(_design, "RoundComplete", new Rect2(650, 138, 620, 172));
        _roundTitle = UiLayout.Place(_roundBanner, UiLayout.Text("", 25, HonorTheme.Brass, true), new Rect2(24, 16, 530, 40));
        UiLayout.Place(_roundBanner, UiLayout.Text(L.Text("Free time — explore and collect your loot."), 18), new Rect2(24, 62, 572, 36));
        UiLayout.Place(_roundBanner, UiLayout.Button(L.Text("Continue exploring"), () => _dismissedRound = _arena.RoundNumber), new Rect2(24, 110, 270, 44));
        UiLayout.Place(_roundBanner, UiLayout.Button(L.Text("Next round"), () => _arena.StartNextRound(), true), new Rect2(310, 110, 286, 44));
        _roundBanner.Hide();
        _nextRound = UiLayout.Place(_design, UiLayout.Button(L.Text("Next round"), () => _arena.StartNextRound()), new Rect2(1550, 372, 333, 48));
        _nextRound.Name = "NextRound";
        _nextRound.Hide();
        _lootPrompt = UiLayout.Place(_design, UiLayout.Text("", 18, HonorTheme.Brass), new Rect2(500, 880, 920, 44));
        _lootPrompt.Name = "LootPrompt";
        _lootPrompt.HorizontalAlignment = HorizontalAlignment.Center;
        _lootPrompt.AutowrapMode = TextServer.AutowrapMode.WordSmart;
    }

    private void RefreshRoundUi(bool alive)
    {
        _roundBanner.Position = new Vector2((_design.Size.X - 620) / 2, 138);
        _lootPrompt.Position = new Vector2((_design.Size.X - 920) / 2, 866);
        _nextRound.Position = new Vector2(_design.Size.X - 370, 372);
        var available = _arena.RoundsEnabled && alive && !_pause.Visible && !_settings.Visible && !_reference.Visible;
        _roundBanner.Visible = available && _arena.RoundComplete && _dismissedRound != _arena.RoundNumber;
        _nextRound.Visible = available && _arena.RoundComplete;
        _lootPrompt.Visible = available && !_inventory.Visible && !_character.Visible;

        if (!_arena.RoundsEnabled)
        {
            return;
        }

        L.SetText(_roundTitle, L.Format($"Round {_arena.RoundNumber} complete"));
        var nearby = _arena.NearestDrop();
        L.SetText(_lootPrompt, _arena.PickupFeedback.Length > 0 ? _arena.PickupFeedback
            : nearby is null ? "" : L.Format($"{GameInputBindings.Name("rfh_pickup")}  Pick up {L.Text(nearby.Item.Base.Name)}"));

        if (alive)
        {
            L.SetText(_objective, _arena.RoundComplete ? L.Text("Free time")
                : L.Format($"Round {_arena.RoundNumber} · Defeated {_arena.DefeatedCount}/{GreyboxArena.EnemiesPerRound}"));
        }
    }
}
