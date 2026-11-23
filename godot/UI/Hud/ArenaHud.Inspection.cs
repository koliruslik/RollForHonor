using System;
using System.Linq;
using Godot;
using RollForHonor.Godot.Gameplay.Combat;
using RollForHonor.Godot.Gameplay.Input;
using RollForHonor.Godot.Gameplay.Targeting;
using RollForHonor.Godot.UI.Inventory;
using RollForHonor.Domain.Items.Models;

namespace RollForHonor.Godot.UI.Hud;

public partial class ArenaHud
{
    private void RefreshTarget()
    {
        var selection = _arena.Player.GetRequiredComponent<CombatAwareness>();
        var id = selection.Selection.TargetId;
        var panel = _design.GetNode<Panel>("Enemy");
        panel.Visible = id is not null && _bodies.ContainsKey(id) && !_runtime.CombatState.Read(_arena.Player.CombatantId, id).Target.IsDefeated;

        if (!panel.Visible)
        {
            return;
        }

        var target = _runtime.CombatState.Read(_arena.Player.CombatantId, id).Target;
        _enemyName.Text = _bodies[id].Name;
        _enemyHealth.MaxValue = target.MaxHealth;
        _enemyHealth.Value = target.Health;
        _enemyAmount.Text = $"{target.Health} / {target.MaxHealth}";
        _enemyState.Text = selection.IsInCombat ? "IN COMBAT" : "SELECTED";
    }

    private void RefreshHover()
    {
        var detailed = global::Godot.Input.IsActionPressed("rfh_details");
        _details.SetDetailed(detailed);
        _comparison.SetDetailed(detailed);
        _itemTip.SetDetailed(detailed);

        if (!_inventory.Visible || _pause.Visible || _settings.Visible || _reference.Visible || (_hoveredItem is null && !_itemTip.IsInspecting))
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
            $"{Binding("arena_forward")} {Binding("arena_left")} {Binding("arena_back")} {Binding("arena_right")}  Move",
            $"{Binding("rfh_zoom_in")} / {Binding("rfh_zoom_out")}  Zoom",
            $"{Binding("rfh_inventory")}  Inventory",
            $"{Binding("rfh_character")}  Character",
            $"{Binding("rfh_menu")}  Menu",
            $"{Binding("rfh_reference")}  Reference",
            $"{Binding("rfh_primary")}  Attack",
            $"{Binding("rfh_target")}  Select / clear target"
        ]);
    }

    private static string Binding(string action) => GameInputBindings.Name(action);

    private void LaunchPrimary()
    {
        var id = _arena.Player.GetRequiredComponent<CombatAwareness>().Selection.TargetId;
        var point = id is not null && _bodies.TryGetValue(id, out var body) ? ((ICombatBody)body).AimPoint
            : _arena.Player.GlobalPosition - _arena.Player.GetNode<Node3D>("Visual").GlobalBasis.Z * 10 + Vector3.Up;
        _arena.Player.GetRequiredComponent<ProjectileAttack>().RequestAttack(point);
    }
}
