using System.Collections.Generic;
using Godot;
using RollForHonor.Godot.UI.Common;
using RollForHonor.Godot.UI.Reference;

namespace RollForHonor.Godot.UI.Hud;

public partial class ArenaHud
{
    private ReferencePanel _reference;

    /// <summary>Toggles the searchable reference, pausing the world and capturing current owned items and combat history.</summary>
    public void ToggleReference() => _navigation.ToggleReference();

    private IReadOnlyList<ReferenceEntry> CaptureReference()
    {
        var read = _runtime.CombatState.ReadEquipment(_arena.Player.CombatantId);

        return ReferenceCatalog.Create(read.OwnedItems.Values, _history.CreateDiceHistoryTopic(), _history.CreateActionHistoryTopic(),
            _runtime.CombatState.GetStats(_arena.Player.CombatantId).Get(new(RollForHonor.Domain.Stats.StatType.Luck)));
    }

    private void BuildReference()
    {
        _reference = UiLayout.Place(_design, new ReferencePanel { Name = "Reference" }, new Rect2(460, 90, 1000, 900));
        _reference.InspectionRequested += OpenInspection;
    }
}
