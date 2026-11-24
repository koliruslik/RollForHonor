using Godot;
using RollForHonor.Godot.Gameplay.Input;
using RollForHonor.Godot.UI.Inventory;
using RollForHonor.Domain.Items.Models;
using RollForHonor.Godot.UI.Localization;

namespace RollForHonor.Godot.UI.Hud;

public partial class ArenaHud
{
    private void RefreshHover()
    {
        var detailed = global::Godot.Input.IsActionPressed("rfh_details");
        _details.SetDetailed(detailed);
        _comparison.SetDetailed(detailed);
        _itemTip.SetDetailed(detailed);

        if (GetViewport().GuiIsDragging() || (!_inventory.Visible && _rounds.SelectedLootId is null) || _pause.Visible || _settings.Visible || _reference.Visible || (_hoveredItem is null && !_itemTip.IsInspecting))
        {
            _itemTip.Hide();
        }
    }

    private void HoverItem(ItemInstance item)
    {
        _hoveredItem = item;

        if (item is null)
        {
            if (!_itemTip.IsInspecting)
            {
                _itemTip.Hide();
            }

            return;
        }

        var read = _runtime.CombatState.ReadEquipment(_arena.Player.CombatantId);
        _itemTip.ShowItem(item, ItemPresentation.Slot(item, read.Rules), false, null);
        RefreshHover();
    }

    private void RefreshHints()
    {
        _hints.GetParent<Control>().Visible = !_inventory.Visible && !_character.Visible && !_pause.Visible && !_settings.Visible && !_reference.Visible;
        _hints.Text = string.Join("\n",
        [
            L.Format($"{Binding("arena_forward")} {Binding("arena_left")} {Binding("arena_back")} {Binding("arena_right")}  Move"),
            L.Format($"{Binding("rfh_zoom_in")} / {Binding("rfh_zoom_out")}  Zoom"),
            L.Format($"{Binding("rfh_inventory")}  Inventory"),
            L.Format($"{Binding("rfh_character")}  Character"),
            L.Format($"{Binding("rfh_menu")}  Menu"),
            L.Format($"{Binding("rfh_reference")}  Reference"),
            L.Format($"{Binding("rfh_primary")}  Attack"),
            L.Format($"{Binding("rfh_target")}  Select / clear target")
        ]);
    }

    private static string Binding(string action) => GameInputBindings.Name(action);

    private void LaunchPrimary() => _combat.LaunchPrimary();
}
