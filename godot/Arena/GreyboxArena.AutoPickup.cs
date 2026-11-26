using System;
using Godot;
using RollForHonor.Godot.UI.Hud;
using RollForHonor.Godot.UI.Localization;

namespace RollForHonor.Godot.Arena;

public partial class GreyboxArena
{
    /// <summary>Gets or sets whether playable rounds automatically collect reachable gold; items always require manual pickup.</summary>
    [Export]
    public bool AutoGoldPickupEnabled { get; set; } = true;

    private TimeSpan _nextAutoPickup;

    /// <summary>Toggles session-local automatic gold collection and reports the current mode.</summary>
    public void ToggleAutoGoldPickup()
    {
        AutoGoldPickupEnabled = !AutoGoldPickupEnabled;
        var message = L.Text(AutoGoldPickupEnabled ? "Automatic gold pickup enabled." : "Automatic gold pickup disabled.");
        _pickupFeedback = message;
        _feedbackUntil = _runtime.GameTime + TimeSpan.FromSeconds(3);
        GetNode<ArenaHud>("Presentation").RecordAction(message);
    }

    private void StepAutoGoldPickup()
    {
        if (!AutoGoldPickupEnabled || NeedsStarterWeapon || GetTree().Paused || GetViewport().GuiIsDragging() || !PlayerIsAlive()
            || _runtime.GameTime < _nextAutoPickup)
        {
            return;
        }

        _nextAutoPickup = _runtime.GameTime + TimeSpan.FromSeconds(0.25);

        foreach (var drop in NearbyGoldDrops())
        {
            PickUpGold(drop.LootId);
        }

    }
}
