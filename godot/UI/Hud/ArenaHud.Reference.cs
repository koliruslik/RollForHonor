using Godot;
using RollForHonor.Godot.UI.Common;
using RollForHonor.Godot.UI.Reference;

namespace RollForHonor.Godot.UI.Hud;

public partial class ArenaHud
{
    private ReferencePanel _reference;
    private bool _referenceReturnToPause;

    /// <summary>Toggles the searchable reference, pausing the world and capturing current owned items and combat history.</summary>
    public void ToggleReference()
    {
        if (_settings.Visible)
        {
            return;
        }

        if (_reference.Visible)
        {
            _reference.Close();
            return;
        }

        _referenceReturnToPause = _pause.Visible;
        _pause.Hide();
        _itemTip.Hide();
        _textTip.Hide();
        _inspector.Clear();
        var read = _runtime.CombatState.ReadEquipment(_arena.Player.CombatantId);
        _reference.Open(ReferenceCatalog.Create(read.OwnedItems.Values, _history.CreateDiceHistoryTopic(), _history.CreateActionHistoryTopic()));
        UpdatePause();
    }

    private void BuildReference()
    {
        _reference = UiLayout.Place(_design, new ReferencePanel { Name = "Reference" }, new Rect2(460, 90, 1000, 900));
        _reference.InspectionRequested += OpenInspection;
        _reference.Closed += () =>
        {
            _inspector.Clear();
            _pause.Visible = _referenceReturnToPause;
            UpdatePause();
        };
    }
}
