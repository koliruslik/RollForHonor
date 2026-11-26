using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using RollForHonor.Domain.Items.Models;
using RollForHonor.Godot.Gameplay.Items;
using RollForHonor.Godot.Gameplay.Input;
using RollForHonor.Godot.UI.Common;
using RollForHonor.Godot.UI.Localization;

namespace RollForHonor.Godot.UI.Inventory;

/// <summary>Retains nearby rows during movement and dragging, with keyboard selection and direct mouse pickup.</summary>
public partial class NearbyLootPanel : Control
{
    /// <summary>Gets the selected nearby item or gold identity used by G.</summary>
    public string SelectedItemId { get; private set; }

    /// <summary>Raised when the player toggles automatic gold collection.</summary>
    public event Action AutoGoldToggleRequested;

    /// <summary>Raised when a row is clicked for pickup.</summary>
    public event Action<string> PickupRequested;

    /// <summary>Raised when an item is hovered for inspection, or null after leaving a row.</summary>
    public event Action<ItemInstance> ItemHovered;

    /// <summary>Gets or sets the icon selector using current equipment compatibility.</summary>
    public Func<ItemInstance, string> IconKey { get; set; }

    private readonly Dictionary<string, Button> _entries = new(StringComparer.Ordinal);
    private VBoxContainer _rows;
    private Panel _panel;
    private ScrollContainer _scroll;
    private string _highlightedId;
    private Button _autoGold;

    /// <inheritdoc />
    public override void _Ready()
    {
        _panel = UiLayout.Panel(this, "Panel", new Rect2(0, 0, 320, 160));
        _panel.AddThemeStyleboxOverride("panel", HonorTheme.Panel(new Color(HonorTheme.Ink, 0.58f), new Color(HonorTheme.Border, 0.35f), 8));
        UiLayout.Place(_panel, UiLayout.Text(L.Text("Nearby loot"), 16, HonorTheme.Brass, true), new Rect2(12, 6, 296, 26));
        UiLayout.Place(_panel, UiLayout.Text(L.Text("G · Click · Drag items into bag"), 11, HonorTheme.Muted), new Rect2(12, 32, 296, 22));
        _autoGold = UiLayout.Place(_panel, new Button { Name = "AutoGold", ToggleMode = true, FocusMode = FocusModeEnum.None }, new Rect2(158, 8, 150, 24));
        _autoGold.AddThemeFontSizeOverride("font_size", 11);

        foreach (var state in new[] { "normal", "hover", "pressed", "focus" })
        {
            _autoGold.AddThemeStyleboxOverride(state, HonorTheme.Panel(new Color(HonorTheme.Ink, 0.2f), new Color(HonorTheme.Brass, 0.45f), 2));
        }

        _autoGold.Pressed += () => AutoGoldToggleRequested?.Invoke();
        _scroll = UiLayout.Place(_panel, new ScrollContainer { HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled, MouseForcePassScrollEvents = false }, new Rect2(8, 58, 304, 92));
        _rows = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        _rows.AddThemeConstantOverride("separation", 2);
        _scroll.AddChild(_rows);
        Hide();
    }

    /// <summary>Reconciles current nearby instances without destroying retained drag sources.</summary>
    /// <param name="drops">The currently reachable world items in nearest-first order.</param>
    /// <param name="gold">Optional captured gold rewards, displayed without item dragging.</param>
    /// <param name="autoGoldPickup">The current session automatic-gold mode shown by the toggle.</param>
    public void Refresh(IReadOnlyList<WorldItemDrop> drops, IReadOnlyList<WorldGoldDrop> gold = null, bool autoGoldPickup = false)
    {
        gold ??= [];
        _autoGold.SetPressedNoSignal(autoGoldPickup);
        L.SetText(_autoGold, L.Text(autoGoldPickup ? "Auto gold: On" : "Auto gold: Off"));
        _autoGold.TooltipText = L.Format($"{GameInputBindings.Name("rfh_auto_gold")} · Toggle automatic gold pickup");
        var height = Math.Max(1, Math.Min(drops.Count + gold.Count, 5)) * 36;
        _scroll.Size = new Vector2(304, height);
        Size = _panel.Size = new Vector2(320, 66 + height);
        var ids = drops
            .Select(drop => drop.Item.Id)
            .Concat(gold.Select(drop => drop.LootId))
            .ToHashSet(StringComparer.Ordinal);

        RemoveMissingRows(ids);
        AddItemRows(drops);
        AddGoldRows(gold);
        RefreshSelection(drops, gold, ids);
    }

    private void RemoveMissingRows(HashSet<string> ids)
    {
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
    }

    private void AddItemRows(IReadOnlyList<WorldItemDrop> drops)
    {
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
    }

    private void AddGoldRows(IReadOnlyList<WorldGoldDrop> gold)
    {
        foreach (var drop in gold)
        {
            if (_entries.ContainsKey(drop.LootId))
            {
                continue;
            }

            var id = drop.LootId;
            var row = new Button
            {
                Name = $"GoldRow{drop.Reward.SourceId.Value:N}",
                CustomMinimumSize = new Vector2(288, 34),
                FocusMode = FocusModeEnum.None,
                MouseDefaultCursorShape = CursorShape.PointingHand
            };
            row.SetMeta("gold", true);
            row.AddThemeFontSizeOverride("font_size", 14);
            row.AddThemeStyleboxOverride("normal", NormalStyle(row));
            row.AddThemeStyleboxOverride("hover", HonorTheme.Panel(new Color(HonorTheme.Ink, 0.35f), new Color(HonorTheme.Brass, 0.45f), 2));
            row.AddThemeStyleboxOverride("pressed", HonorTheme.Panel(new Color(HonorTheme.Ink, 0.45f), HonorTheme.Brass, 2));
            row.AddThemeColorOverride("font_color", HonorTheme.Brass);
            row.Pressed += () => PickupRequested?.Invoke(id);
            row.MouseEntered += () =>
            {
                SelectedItemId = id;
                ItemHovered?.Invoke(null);
            };
            _rows.AddChild(row);
            L.SetText(row, L.Format($"Gold: {drop.Reward.Amount}"));
            _entries[id] = row;
        }
    }

    private static StyleBoxFlat NormalStyle(Button row) => row.HasMeta("gold")
        ? HonorTheme.Panel(new Color(HonorTheme.Ink, 0.12f), new Color(HonorTheme.Brass, 0.45f), 2)
        : HonorTheme.Panel(new Color(0, 0, 0, 0), new Color(0, 0, 0, 0), 2);

    private void RefreshSelection(IReadOnlyList<WorldItemDrop> drops, IReadOnlyList<WorldGoldDrop> gold, HashSet<string> ids)
    {
        if (SelectedItemId is null || !ids.Contains(SelectedItemId))
        {
            SelectedItemId = drops.FirstOrDefault()?.Item.Id ?? gold.FirstOrDefault()?.LootId;
        }

        if (_highlightedId != SelectedItemId)
        {
            if (_highlightedId is not null && _entries.TryGetValue(_highlightedId, out var old))
            {
                old.AddThemeStyleboxOverride("normal", NormalStyle(old));
            }

            if (SelectedItemId is not null)
            {
                _entries[SelectedItemId].AddThemeStyleboxOverride("normal", HonorTheme.Panel(new Color(HonorTheme.Ink, 0.18f), new Color(HonorTheme.Brass, 0.65f), 2));
            }

            _highlightedId = SelectedItemId;
        }
    }
}
