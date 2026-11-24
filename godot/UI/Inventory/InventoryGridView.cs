using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using RollForHonor.Domain.Items.Inventory.Models;
using RollForHonor.Domain.Items.Inventory.Services;
using RollForHonor.Domain.Items.Models;
using RollForHonor.Godot.UI.Common;

namespace RollForHonor.Godot.UI.Inventory;

/// <summary>Renders a session-local spatial layout backed by deterministic Domain placement rules.</summary>
public partial class InventoryGridView : Control
{
    /// <summary>The square cell edge in design pixels.</summary>
    public const int CellSize = 42;

    /// <summary>The fixed number of inventory columns.</summary>
    public const int Columns = 12;

    /// <summary>The fixed number of inventory rows.</summary>
    public const int Rows = 5;

    /// <summary>Gets the immutable current grid, with twelve columns and five rows.</summary>
    public InventoryGridLayout Layout { get; private set; } = new(Columns, Rows);

    /// <summary>Gets or sets the canonical owner-scoped lookup.</summary>
    public Func<string, ItemInstance> ResolveItem { get; set; }

    /// <summary>Gets or sets the equipped-item membership check.</summary>
    public Func<string, bool> IsEquipped { get; set; }

    /// <summary>Gets or sets the item icon selector.</summary>
    public Func<ItemInstance, string> IconKey { get; set; }

    /// <summary>Raised when an inventory item is selected.</summary>
    public event Action<string> ItemSelected;

    /// <summary>Raised when an inventory item is activated with the right button.</summary>
    public event Action<string> ItemActivated;

    /// <summary>Raised when a pointer enters an item or leaves it with an empty identity.</summary>
    public event Action<string> ItemHovered;

    /// <summary>Raised synchronously to return an equipped item to its requested cells.</summary>
    public event Action<string> UnequipRequested;

    /// <summary>Gets or sets a nearby-world lookup for native bag drops.</summary>
    public Func<string, ItemInstance> ResolveExternalItem { get; set; }

    /// <summary>Gets or sets the capacity-checked pickup into the requested top-left cell.</summary>
    public Func<string, InventoryPosition, bool> ReceiveExternalItem { get; set; }

    private readonly InventoryGridService _placement = new();
    private readonly Dictionary<string, InventoryPosition> _remembered = new();
    private readonly Dictionary<string, InventoryPosition> _requested = new();
    private Dictionary<string, ItemInstance> _items = new(StringComparer.Ordinal);
    private Control _overlay;
    private Rect2 _preview;
    private bool _previewVisible;
    private bool _previewValid;

    /// <inheritdoc />
    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Stop;
        SizeFlagsHorizontal = SizeFlags.ShrinkBegin;
        _overlay = new Control { Name = "DropPreview", MouseFilter = MouseFilterEnum.Ignore };
        AddChild(_overlay);
        _overlay.Draw += DrawPreview;
        UpdateSize();
    }

    /// <inheritdoc />
    public override void _Draw()
    {
        var extent = new Vector2(Layout.Columns, Layout.Rows) * CellSize;
        DrawRect(new Rect2(Vector2.Zero, extent), new Color("101416"));

        for (var row = 0; row < Layout.Rows; row++)
        {
            for (var column = 0; column < Layout.Columns; column++)
            {
                var center = (new Vector2(column, row) + Vector2.One * 0.5f) * CellSize;
                var color = new Color(HonorTheme.Muted, 0.25f);
                DrawLine(center - new Vector2(3, 0), center + new Vector2(3, 0), color, 1);
                DrawLine(center - new Vector2(0, 3), center + new Vector2(0, 3), color, 1);
            }
        }

        for (var column = 1; column < Layout.Columns; column++)
        {
            DrawLine(new Vector2(column * CellSize, 0), new Vector2(column * CellSize, extent.Y), HonorTheme.Border, 1);
        }

        for (var row = 1; row < Layout.Rows; row++)
        {
            DrawLine(new Vector2(0, row * CellSize), new Vector2(extent.X, row * CellSize), HonorTheme.Border, 1);
        }

        DrawRect(new Rect2(Vector2.One * 0.5f, extent - Vector2.One), HonorTheme.Border, false, 1);
    }

    /// <inheritdoc />
    public override void _Notification(int what)
    {
        if (what == NotificationDragEnd && _overlay is not null)
        {
            _previewVisible = false;
            _overlay.QueueRedraw();
        }
    }

    /// <inheritdoc />
    public override bool _CanDropData(Vector2 atPosition, Variant data) => ItemCell.TryReadItem(data, out var id)
        && CanPlaceAt(id, atPosition - ItemCell.ReadGrabCell(data) * CellSize);

    /// <inheritdoc />
    public override void _DropData(Vector2 atPosition, Variant data)
    {
        if (ItemCell.TryReadItem(data, out var id))
        {
            DropAt(id, atPosition - ItemCell.ReadGrabCell(data) * CellSize);
        }
    }

    /// <summary>Checks whether the bag contents fit without moving retained items or adding rows.</summary>
    /// <param name="items">The canonical bag contents after a proposed equipment change.</param>
    /// <returns>True when every item has a complete free rectangle.</returns>
    /// <exception cref="ArgumentNullException">The collection is null.</exception>
    /// <exception cref="ArgumentException">An item is null, too large, or has a duplicated identity.</exception>
    public bool CanFitItems(IReadOnlyList<ItemInstance> items) => TryBuildLayout(items, out _);

    /// <summary>Reconciles bag contents while preserving retained and remembered positions within a fixed grid.</summary>
    /// <param name="items">The current canonical bag contents.</param>
    /// <exception cref="ArgumentNullException">The collection is null.</exception>
    /// <exception cref="ArgumentException">An item is null, too large, or has a duplicated identity.</exception>
    /// <exception cref="InvalidOperationException">The complete contents do not fit; the previous layout is retained.</exception>
    public void RefreshItems(IReadOnlyList<ItemInstance> items)
    {
        if (!TryBuildLayout(items, out var updated))
        {
            throw new InvalidOperationException("Inventory contents do not fit the fixed grid.");
        }

        var removed = Layout.Placements.Values
            .Where(entry => !updated.Placements.ContainsKey(entry.ItemId));

        foreach (var entry in removed)
        {
            _remembered[entry.ItemId] = entry.Position;
        }

        _items = items.ToDictionary(item => item.Id, StringComparer.Ordinal);
        Layout = updated;
        RebuildTiles();
    }

    private void DrawPreview()
    {
        if (!_previewVisible)
        {
            return;
        }

        var color = new Color(_previewValid ? "8fb697" : "bd7770");
        _overlay.DrawRect(_preview, new Color(color, 0.2f));
        _overlay.DrawRect(_preview, color, false, 2);
    }

    private void UpdateSize()
    {
        CustomMinimumSize = Size = new Vector2(Layout.Columns, Layout.Rows) * CellSize;
        _overlay.Size = Size;
        QueueRedraw();
    }

    private bool CanPlaceAt(string id, Vector2 topLeft)
    {
        var item = ResolveItem?.Invoke(id) ?? ResolveExternalItem?.Invoke(id);

        if (item is null || (!_items.ContainsKey(id) && IsEquipped?.Invoke(id) != true && ResolveExternalItem?.Invoke(id) is null))
        {
            return false;
        }

        var position = Cell(topLeft);
        _previewVisible = true;
        _previewValid = _placement.CanPlace(Layout, id, item.Base.Footprint, position);
        _preview = new Rect2(new Vector2(position.Column, position.Row) * CellSize,
            new Vector2(item.Base.Footprint.Width, item.Base.Footprint.Height) * CellSize);
        _overlay.QueueRedraw();

        return _previewValid;
    }

    private static InventoryPosition Cell(Vector2 point) => new(Mathf.FloorToInt(point.X / CellSize), Mathf.FloorToInt(point.Y / CellSize));

    private void DropAt(string id, Vector2 topLeft)
    {
        if (!CanPlaceAt(id, topLeft))
        {
            return;
        }

        var position = Cell(topLeft);

        if (_items.TryGetValue(id, out var item))
        {
            _placement.TryPlace(Layout, id, item.Base.Footprint, position, out var moved);
            Layout = moved;
            _remembered[id] = position;
            RebuildTiles();
        }
        else
        {
            _requested[id] = position;
            try
            {
                if (ResolveExternalItem?.Invoke(id) is not null)
                {
                    ReceiveExternalItem?.Invoke(id, position);
                }
                else
                {
                    UnequipRequested?.Invoke(id);
                }
            }
            finally
            {
                _requested.Remove(id);
            }
        }

        _previewVisible = false;
        _overlay.QueueRedraw();
    }

    private bool TryBuildLayout(IReadOnlyList<ItemInstance> items, out InventoryGridLayout updated)
    {
        ArgumentNullException.ThrowIfNull(items);

        if (items.Any(item => item is null || item.Base.Footprint.Width > Layout.Columns || item.Base.Footprint.Height > Layout.Rows))
        {
            throw new ArgumentException("Inventory items must be nonnull and fit the fixed grid dimensions.", nameof(items));
        }

        var incoming = items.ToDictionary(item => item.Id, StringComparer.Ordinal);
        updated = Layout;

        var removed = Layout.Placements.Values
            .Where(entry => !incoming.ContainsKey(entry.ItemId));

        foreach (var entry in removed)
        {
            updated = _placement.Remove(updated, entry.ItemId);
        }

        foreach (var item in items
            .OrderByDescending(item => item.Base.Footprint.Height)
            .ThenByDescending(item => item.Base.Footprint.Width)
            .ThenBy(item => item.Id, StringComparer.Ordinal))
        {
            if (!TryPlaceItem(updated, item, out updated))
            {
                return false;
            }
        }

        return true;
    }

    private bool TryPlaceItem(InventoryGridLayout layout, ItemInstance item, out InventoryGridLayout updated)
    {
        updated = layout;

        if (layout.Placements.TryGetValue(item.Id, out var existing) && existing.Footprint == item.Base.Footprint)
        {
            return true;
        }

        layout = _placement.Remove(layout, item.Id);

        if ((_requested.TryGetValue(item.Id, out var position) || _remembered.TryGetValue(item.Id, out position))
            && _placement.TryPlace(layout, item.Id, item.Base.Footprint, position, out updated))
        {
            return true;
        }

        return _placement.TryFindSpace(layout, item.Base.Footprint, out position)
            && _placement.TryPlace(layout, item.Id, item.Base.Footprint, position, out updated);
    }

    private void RebuildTiles()
    {
        var old = GetChildren()
            .OfType<ItemCell>()
            .ToArray();

        foreach (var child in old)
        {
            RemoveChild(child);
            child.QueueFree();
        }

        foreach (var entry in Layout.Placements.Values)
        {
            AddTile(entry);
        }

        MoveChild(_overlay, -1);
        UpdateSize();
    }

    private void AddTile(InventoryPlacement entry)
    {
        var item = _items[entry.ItemId];
        var extent = new Vector2(entry.Footprint.Width, entry.Footprint.Height) * CellSize;
        var cell = new ItemCell
        {
            Name = item.Base.Id,
            ItemId = item.Id,
            UseGridGrabOffset = true,
            DragFootprint = new Vector2I(entry.Footprint.Width, entry.Footprint.Height),
            Position = new Vector2(entry.Position.Column, entry.Position.Row) * CellSize,
            Size = extent,
            CustomMinimumSize = extent
        };
        cell.CanReceiveAt = (id, point) => CanPlaceAt(id, cell.Position + point);
        cell.ReceiveAt = (id, point) => DropAt(id, cell.Position + point);
        cell.Pressed += () => ItemSelected?.Invoke(item.Id);
        cell.Activated += () => ItemActivated?.Invoke(item.Id);
        cell.MouseEntered += () => ItemHovered?.Invoke(item.Id);
        cell.MouseExited += () => ItemHovered?.Invoke("");
        AddChild(cell);
        cell.SetAppearance(IconKey?.Invoke(item) ?? "bag", ItemPresentation.RarityColor(item.Rarity), true);
        cell.Size = extent;
    }
}
