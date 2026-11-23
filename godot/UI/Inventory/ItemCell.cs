using System;
using Godot;
using RollForHonor.Godot.UI.Common;

namespace RollForHonor.Godot.UI.Inventory;

/// <summary>A centered item icon with native drag/drop and right-click activation.</summary>
public partial class ItemCell : Button
{
    /// <summary>Gets or sets the owned instance carried by this cell, or an empty identity.</summary>
    public string ItemId { get; set; } = "";

    /// <summary>Gets or sets the current ownership and slot compatibility check for drops.</summary>
    public Func<string, bool> CanReceive { get; set; }

    /// <summary>Gets or sets the operation invoked after a validated drop.</summary>
    public Action<string> Receive { get; set; }

    /// <summary>Gets or sets a position-aware drop check receiving the candidate top-left pixel in this control.</summary>
    public Func<string, Vector2, bool> CanReceiveAt { get; set; }

    /// <summary>Gets or sets the matching position-aware operation.</summary>
    public Action<string, Vector2> ReceiveAt { get; set; }

    /// <summary>Gets or sets the inventory dimensions used by the native drag preview.</summary>
    public Vector2I DragFootprint { get; set; } = Vector2I.One;

    /// <summary>Gets or sets whether the grab offset is measured inside a multicell inventory rectangle.</summary>
    public bool UseGridGrabOffset { get; set; }

    /// <summary>Raised when an occupied cell receives a right click.</summary>
    public event Action Activated;

    private TextureRect _image;

    /// <inheritdoc />
    public override void _Ready()
    {
        FocusMode = FocusModeEnum.None;
        MouseDefaultCursorShape = CursorShape.PointingHand;
        _image = UiLayout.Icon("bag");
        AddChild(_image);
        _image.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        _image.OffsetLeft = _image.OffsetTop = 12;
        _image.OffsetRight = _image.OffsetBottom = -12;
    }

    /// <inheritdoc />
    public override void _GuiInput(InputEvent @event)
    {
        if (@event is InputEventMouseButton { ButtonIndex: MouseButton.Right, Pressed: true }
            && !string.IsNullOrEmpty(ItemId))
        {
            AcceptEvent();
            Activated?.Invoke();
        }
    }

    /// <inheritdoc />
    public override Variant _GetDragData(Vector2 atPosition)
    {
        if (string.IsNullOrEmpty(ItemId))
        {
            return default;
        }

        var size = new Vector2(DragFootprint.X, DragFootprint.Y) * InventoryGridView.CellSize;
        var preview = new Control { Size = size, CustomMinimumSize = size, MouseFilter = MouseFilterEnum.Ignore };
        var frame = UiLayout.Panel(preview, "Frame", new Rect2(Vector2.Zero, size));
        frame.MouseFilter = MouseFilterEnum.Ignore;
        frame.AddThemeStyleboxOverride("panel", GetThemeStylebox("normal"));
        var icon = UiLayout.Place(frame, UiLayout.Icon("bag"), new Rect2(Vector2.One * 12, size - Vector2.One * 24));
        icon.Texture = _image.Texture;
        SetDragPreview(preview);
        preview.Position = UseGridGrabOffset ? -atPosition : -Vector2.One * (InventoryGridView.CellSize / 2f);
        var grab = UseGridGrabOffset
            ? new Vector2I(Mathf.Clamp(Mathf.FloorToInt(atPosition.X / InventoryGridView.CellSize), 0, DragFootprint.X - 1),
                Mathf.Clamp(Mathf.FloorToInt(atPosition.Y / InventoryGridView.CellSize), 0, DragFootprint.Y - 1))
            : Vector2I.Zero;

        return new global::Godot.Collections.Dictionary { ["rfh-item-id"] = ItemId, ["rfh-grab-cell"] = grab };
    }

    /// <inheritdoc />
    public override bool _CanDropData(Vector2 atPosition, Variant data) =>
        TryReadItem(data, out var id) && Accepts(id, atPosition, data);

    /// <inheritdoc />
    public override void _DropData(Vector2 atPosition, Variant data)
    {
        if (!TryReadItem(data, out var id) || !Accepts(id, atPosition, data))
        {
            return;
        }

        if (CanReceiveAt is not null)
        {
            ReceiveAt?.Invoke(id, atPosition - ReadGrabCell(data) * InventoryGridView.CellSize);
        }
        else
        {
            Receive?.Invoke(id);
        }
    }

    /// <summary>Updates the icon and rarity frame without relying on Button's text/icon alignment.</summary>
    /// <param name="icon">The SVG asset key.</param>
    /// <param name="color">The item's rarity color, or a neutral empty-slot color.</param>
    /// <param name="occupied">Whether the slot contains an owned item.</param>
    public void SetAppearance(string icon, Color color, bool occupied)
    {
        _image.Texture = GD.Load<Texture2D>($"res://UI/Assets/{icon}.svg");
        _image.Modulate = occupied ? Colors.White : new Color(1, 1, 1, 0.18f);
        AddThemeStyleboxOverride("normal", HonorTheme.Panel(new Color(occupied ? "222b30" : "101416"), color, 0));
        var marker = GetNodeOrNull<Label>("EmptyMarker");

        if (marker is null)
        {
            marker = UiLayout.Text("+", 16, HonorTheme.Muted);
            marker.Name = "EmptyMarker";
            marker.HorizontalAlignment = HorizontalAlignment.Center;
            AddChild(marker);
            marker.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        }

        marker.Visible = !occupied;
        AddThemeStyleboxOverride("hover", HonorTheme.Panel(new Color("303336"), color.Lightened(0.2f), 0));
        AddThemeStyleboxOverride("pressed", HonorTheme.Panel(new Color("393c3e"), color, 0));
    }

    internal static bool TryReadItem(Variant data, out string id)
    {
        id = "";

        if (data.VariantType != Variant.Type.Dictionary)
        {
            return false;
        }

        var payload = data.AsGodotDictionary();

        if (!payload.TryGetValue("rfh-item-id", out var value) || value.VariantType != Variant.Type.String)
        {
            return false;
        }

        id = value.AsString();

        return !string.IsNullOrEmpty(id);
    }

    internal static Vector2 ReadGrabCell(Variant data)
    {
        var payload = data.AsGodotDictionary();

        if (payload.TryGetValue("rfh-grab-cell", out var value) && value.VariantType == Variant.Type.Vector2I)
        {
            var offset = value.AsVector2I();

            if (offset.X >= 0 && offset.Y >= 0)
            {
                return new Vector2(offset.X, offset.Y);
            }
        }

        return Vector2.Zero;
    }

    private bool Accepts(string id, Vector2 position, Variant data) => CanReceiveAt is not null
        ? CanReceiveAt(id, position - ReadGrabCell(data) * InventoryGridView.CellSize)
        : CanReceive?.Invoke(id) == true;
}
