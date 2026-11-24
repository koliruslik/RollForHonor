using Godot;
using RollForHonor.Domain.Items.Models;
using RollForHonor.Godot.UI.Common;
using RollForHonor.Godot.UI.Localization;

namespace RollForHonor.Godot.UI.Inventory;

/// <summary>A clickable nearby item with a native drag payload for the fixed bag.</summary>
public partial class LootEntry : Button
{
    /// <summary>Gets or sets the captured item before tree entry.</summary>
    public ItemInstance Item { get; set; }

    /// <summary>Gets or sets the compatible item icon key before tree entry.</summary>
    public string IconKey { get; set; } = "bag";

    /// <inheritdoc />
    public override void _Ready()
    {
        FocusMode = FocusModeEnum.None;
        CustomMinimumSize = new Vector2(380, 48);
        MouseDefaultCursorShape = CursorShape.PointingHand;
        var color = ItemPresentation.RarityColor(Item.Rarity);
        var icon = UiLayout.Place(this, UiLayout.Icon(IconKey), new Rect2(10, 8, 32, 32));
        icon.Modulate = color;
        var label = UiLayout.Place(this, UiLayout.Text(L.Text(Item.Base.Name), 17, color), new Rect2(54, 4, 304, 40));
        label.ClipText = true;
        TooltipText = L.Text(Item.Base.Name);
        L.OnChanged(this, () => TooltipText = L.Text(Item.Base.Name));
    }

    /// <inheritdoc />
    public override Variant _GetDragData(Vector2 atPosition)
    {
        var preview = UiLayout.Text(L.Text(Item.Base.Name), 18, ItemPresentation.RarityColor(Item.Rarity));
        SetDragPreview(preview);

        return new global::Godot.Collections.Dictionary { ["rfh-item-id"] = Item.Id, ["rfh-grab-cell"] = Vector2I.Zero };
    }
}
