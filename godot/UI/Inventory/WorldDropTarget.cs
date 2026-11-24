using System;
using Godot;

namespace RollForHonor.Godot.UI.Inventory;

/// <summary>An invisible native drag destination behind the HUD, active only for owned inventory or equipment drags.</summary>
public partial class WorldDropTarget : Control
{
    /// <summary>Gets or sets the current canonical owned-item release check.</summary>
    public Func<string, bool> CanRelease { get; set; }

    /// <summary>Gets or sets the authoritative release operation.</summary>
    public Func<string, bool> Release { get; set; }

    /// <inheritdoc />
    public override bool _CanDropData(Vector2 atPosition, Variant data) =>
        ItemCell.TryReadItem(data, out var id) && CanRelease?.Invoke(id) == true;

    /// <inheritdoc />
    public override void _DropData(Vector2 atPosition, Variant data)
    {
        if (_CanDropData(atPosition, data) && ItemCell.TryReadItem(data, out var id))
        {
            Release?.Invoke(id);
        }
    }
}
