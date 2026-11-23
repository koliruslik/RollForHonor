using System.Linq;
using Godot;
using RollForHonor.Godot.UI.Localization;

namespace RollForHonor.Godot.Gameplay.Input;

/// <summary>Shares action-map defaults and readable current binding names between controls and hints.</summary>
public static class GameInputBindings
{
    /// <summary>Registers missing interface actions while preserving existing user bindings.</summary>
    public static void EnsureDefaults()
    {
        Add("rfh_inventory", Key.I);
        Add("rfh_character", Key.C);
        Add("rfh_reference", Key.H);
        Add("rfh_menu", Key.Escape);
        Add("rfh_details", Key.Alt);
        Add("rfh_skill2", Key.Q);
        Add("rfh_skill3", Key.E);
        Add("rfh_skill4", Key.F);
        AddMouse("rfh_primary", MouseButton.Right);
        AddMouse("rfh_target", MouseButton.Left);
        AddMouse("rfh_zoom_in", MouseButton.WheelUp);
        AddMouse("rfh_zoom_out", MouseButton.WheelDown);
    }

    /// <summary>Reads the current key for an action.</summary>
    /// <param name="action">A registered action identifier.</param>
    /// <returns>The key name, or a dash if no key is bound.</returns>
    public static string Name(string action)
    {
        var events = InputMap.ActionGetEvents(action);
        var key = events
            .OfType<InputEventKey>()
            .FirstOrDefault();

        if (key is not null)
        {
            var code = key.PhysicalKeycode == Key.None ? key.Keycode : key.PhysicalKeycode;

            return code == Key.Escape ? "Esc" : OS.GetKeycodeString(code);
        }

        return events
            .OfType<InputEventMouseButton>()
            .FirstOrDefault()?.ButtonIndex switch
        {
            MouseButton.Right => L.Text("RMB"),
            MouseButton.Left => L.Text("LMB"),
            MouseButton.WheelUp => L.Text("Wheel up"),
            MouseButton.WheelDown => L.Text("Wheel down"),
            _ => "—"
        };
    }

    private static void AddMouse(string action, MouseButton button)
    {
        if (!InputMap.HasAction(action))
        {
            InputMap.AddAction(action);
            InputMap.ActionAddEvent(action, new InputEventMouseButton { ButtonIndex = button });
        }
    }

    private static void Add(string action, Key key)
    {
        if (!InputMap.HasAction(action))
        {
            InputMap.AddAction(action);
            InputMap.ActionAddEvent(action, new InputEventKey { PhysicalKeycode = key });
        }
    }
}
