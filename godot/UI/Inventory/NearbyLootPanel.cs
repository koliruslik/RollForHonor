using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using RollForHonor.Domain.Items.Models;
using RollForHonor.Godot.Gameplay.Items;
using RollForHonor.Godot.UI.Common;
using RollForHonor.Godot.UI.Localization;

namespace RollForHonor.Godot.UI.Inventory;

/// <summary>Retains nearby rows during movement and dragging, with keyboard selection and direct mouse pickup.</summary>
public partial class NearbyLootPanel : Control
{
    /// <summary>Gets the selected nearby identity used by G.</summary>
    public string SelectedItemId { get; private set; }

    /// <summary>Raised when a row is clicked for pickup.</summary>
    public event Action<string> PickupRequested;

    /// <summary>Raised when an item is hovered for inspection, or null after leaving a row.</summary>
    public event Action<ItemInstance> ItemHovered;

    /// <summary>Gets or sets the icon selector using current equipment compatibility.</summary>
    public Func<ItemInstance, string> IconKey { get; set; }

    private readonly Dictionary<string, LootEntry> _entries = new(StringComparer.Ordinal);
    private VBoxContainer _rows;
    private string _highlightedId;

    /// <inheritdoc />
    public override void _Ready()
    {
        var panel = UiLayout.Panel(this, "Panel", new Rect2(0, 0, 420, 320));
        UiLayout.Place(panel, UiLayout.Text(L.Text("Nearby loot"), 22, HonorTheme.Brass, true), new Rect2(16, 8, 388, 36));
        var hints = UiLayout.Place(panel, UiLayout.Text(L.Text("G  Pick selected · Click to take · Drag into bag"), 13, HonorTheme.Muted), new Rect2(16, 46, 388, 40));
        hints.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        var scroll = UiLayout.Place(panel, new ScrollContainer { HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled }, new Rect2(14, 92, 392, 212));
        _rows = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        scroll.AddChild(_rows);
        Hide();
    }

    /// <summary>Reconciles current nearby instances without destroying retained drag sources.</summary>
    /// <param name="drops">The currently reachable world items in nearest-first order.</param>
    public void Refresh(IReadOnlyList<WorldItemDrop> drops)
    {
        var ids = drops
            .Select(drop => drop.Item.Id)
            .ToHashSet(StringComparer.Ordinal);

        foreach (var id in _entries.Keys
            .Where(id => !ids.Contains(id))
            .ToArray())
        {
            var row = _entries[id];
            _rows.RemoveChild(row);
            row.QueueFree();
            _entries.Remove(id);
            ItemHovered?.Invoke(null);
        }

        foreach (var drop in drops)
        {
            if (_entries.ContainsKey(drop.Item.Id))
            {
                continue;
            }

            var item = drop.Item;
            var row = new LootEntry { Name = $"Loot{_entries.Count}", Item = item, IconKey = IconKey?.Invoke(item) ?? "bag" };
            row.Pressed += () => PickupRequested?.Invoke(item.Id);
            row.MouseEntered += () =>
            {
                SelectedItemId = item.Id;
                ItemHovered?.Invoke(item);
            };
            row.MouseExited += () => ItemHovered?.Invoke(null);
            _rows.AddChild(row);
            _entries[item.Id] = row;
        }

        if (SelectedItemId is null || !ids.Contains(SelectedItemId))
        {
            SelectedItemId = drops.FirstOrDefault()?.Item.Id;
        }

        if (_highlightedId != SelectedItemId)
        {
            if (_highlightedId is not null && _entries.TryGetValue(_highlightedId, out var old))
            {
                old.RemoveThemeStyleboxOverride("normal");
            }

            if (SelectedItemId is not null)
            {
                _entries[SelectedItemId].AddThemeStyleboxOverride("normal", HonorTheme.Panel(HonorTheme.Ink, HonorTheme.Brass));
            }

            _highlightedId = SelectedItemId;
        }
    }
}
