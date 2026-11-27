using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Godot;
using RollForHonor.Application.Items;
using RollForHonor.Domain.Items.Affixes.Models;
using RollForHonor.Domain.Items.Models;
using RollForHonor.Godot.Arena;
using RollForHonor.Godot.Content.Items;
using RollForHonor.Godot.Content.Loot;
using RollForHonor.Godot.UI.Common;
using RollForHonor.Godot.UI.Hud;
using RollForHonor.Godot.UI.Inventory;
using RollForHonor.Godot.UI.Localization;

namespace RollForHonor.Godot.UI.Admin;

/// <summary>Creates configured items or edits an owned snapshot through available catalog families and explicit quality values.</summary>
public partial class AdminItemEditor : Control
{
    /// <summary>Gets or sets whether this page edits an existing item rather than creating a new one.</summary>
    public bool EditExisting { get; set; }

    /// <summary>Raised with localized action results for the containing admin panel.</summary>
    public event Action<string> StatusChanged;

    private GameBootstrap _runtime;
    private GreyboxArena _arena;
    private OptionButton _items;
    private OptionButton _rarity;
    private SpinBox _level;
    private Label _caption;
    private ItemCell _drop;
    private VBoxContainer _rows;
    private Button _apply;
    private ItemInstance _source;
    private ItemInstance _expected;
    private string[] _itemIds = [];
    private bool _loading;

    /// <inheritdoc />
    public override void _Ready()
    {
        _items = UiLayout.Place(this, new OptionButton { Name = "Item", FitToLongestItem = false }, new Rect2(0, 0, 660, 36));
        _items.ItemSelected += _ => SelectItem();
        _caption = UiLayout.Place(this, UiLayout.Text(L.Text(EditExisting ? "Drop an inventory item here to edit it." : "Choose a base, rarity and affixes."), 14), new Rect2(0, 42, 560, 34));
        _drop = new ItemCell
        {
            Name = "DropTarget",
            CanReceive = id => EditExisting && _runtime is not null
                && _runtime.CombatState.ReadEquipment(_arena.Player.CombatantId).OwnedItems.ContainsKey(id),
            Receive = id => LoadOwnedItem(id)
        };
        UiLayout.Place(this, _drop, new Rect2(584, 42, 76, 68));
        _drop.Visible = EditExisting;
        UiLayout.Place(this, UiLayout.Text(L.Text("Rarity"), 14), new Rect2(0, 82, 78, 30));
        _rarity = UiLayout.Place(this, new OptionButton { Name = "Rarity", FitToLongestItem = false }, new Rect2(80, 80, 220, 36));

        foreach (var rarity in Enum.GetValues<ItemRarity>())
        {
            _rarity.AddItem(L.Text(rarity.ToString()), (int)rarity);
        }

        UiLayout.Place(this, UiLayout.Text(L.Text("ilvl"), 14), new Rect2(320, 82, 48, 30));
        _level = UiLayout.Place(this, new SpinBox { Name = "ItemLevel", MinValue = 1, MaxValue = 100, Step = 1, Value = 1 }, new Rect2(370, 80, 120, 36));
        _rarity.ItemSelected += _ => RebuildRows();
        _level.ValueChanged += _ => RebuildRows();
        UiLayout.Place(this, UiLayout.Text(L.Text("Affix"), 14, HonorTheme.Muted), new Rect2(0, 126, 360, 26));
        UiLayout.Place(this, UiLayout.Text(L.Text("Tier"), 14, HonorTheme.Muted), new Rect2(390, 126, 110, 26));
        UiLayout.Place(this, UiLayout.Text(L.Text("Quality %"), 14, HonorTheme.Muted), new Rect2(530, 126, 125, 26));
        var scroll = UiLayout.Place(this, new ScrollContainer { Name = "AffixScroll", HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled }, new Rect2(0, 156, 660, 328));
        _rows = new VBoxContainer { Name = "AffixRows", SizeFlagsHorizontal = SizeFlags.ExpandFill };
        _rows.AddThemeConstantOverride("separation", 6);
        scroll.AddChild(_rows);
        _apply = UiLayout.Place(this, UiLayout.Button(L.Text(EditExisting ? "Save item" : "Create item"), Apply, true), new Rect2(0, 498, 660, 42));
        _apply.Name = "Apply";
        _apply.Disabled = true;
    }

    /// <summary>Connects this editor to the active sandbox session.</summary>
    /// <param name="runtime">The authoritative session.</param>
    /// <param name="arena">The active arena and inventory host.</param>
    public void Configure(GameBootstrap runtime, GreyboxArena arena)
    {
        _runtime = runtime;
        _arena = arena;
    }

    /// <summary>Refreshes available owned items or released bases without mutating a draft's captured source.</summary>
    public void Open()
    {
        _items.Clear();

        foreach (var rarity in Enum.GetValues<ItemRarity>())
        {
            _rarity.SetItemText((int)rarity, L.Text(rarity.ToString()));
        }

        if (EditExisting)
        {
            var owned = _runtime.CombatState.ReadEquipment(_arena.Player.CombatantId).OwnedItems.Values.ToArray();
            _itemIds = owned
                .Select(item => item.Id)
                .ToArray();
            _items.AddItem(L.Text("Select an owned item…"));

            foreach (var item in owned)
            {
                _items.AddItem(ItemPresentation.Name(item));
            }
        }
        else
        {
            var bases = GameLootCatalog.Bases
                .Where(entry => entry.GetProperty("catalog_visible").GetBoolean() && entry.GetProperty("released").GetBoolean())
                .ToArray();
            _itemIds = bases
                .Select(entry => entry.GetProperty("id").GetString())
                .ToArray();

            foreach (var entry in bases)
            {
                _items.AddItem(L.Text(entry.GetProperty("name").GetString()));
            }

            SelectItem();
        }
    }

    /// <summary>Loads an owned item into the editing draft without removing it from inventory or equipment.</summary>
    /// <param name="id">The owned instance identity supplied by the native inventory drag payload.</param>
    /// <returns>True if the currently owned snapshot was loaded.</returns>
    public bool LoadOwnedItem(string id)
    {
        if (!EditExisting || !_runtime.CombatState.ReadEquipment(_arena.Player.CombatantId).OwnedItems.TryGetValue(id, out var item))
        {
            return false;
        }

        _loading = true;
        _expected = _source = item;
        _rarity.Select((int)item.Rarity);
        _level.Value = item.GenerationLevel;
        _loading = false;
        _drop.ItemId = "";
        _drop.SetAppearance(ItemPresentation.Icon(item, ItemPresentation.Slot(item,
            _runtime.CombatState.ReadEquipment(_arena.Player.CombatantId).Rules)), ItemPresentation.RarityColor(item.Rarity), true);
        L.SetText(_caption, ItemPresentation.Name(item));
        RebuildRows(item.Affixes
            .Select(affix => new AdminAffixChoice(affix.DefinitionId, affix.TierId, affix.EffectiveQualityRoll, affix))
            .ToArray());
        return true;
    }

    private void SelectItem()
    {
        if (_loading || _itemIds.Length == 0)
        {
            return;
        }

        if (EditExisting)
        {
            if (_items.Selected > 0)
            {
                LoadOwnedItem(_itemIds[_items.Selected - 1]);
            }

            return;
        }

        _loading = true;
        var entry = GameLootCatalog.Bases.Single(value => value.GetProperty("id").GetString() == _itemIds[_items.Selected]);
        _level.MinValue = entry.GetProperty("item_level").GetInt32();
        _level.Value = Math.Max(_level.Value, _level.MinValue);
        _source = AdminItemCatalog.Empty(_itemIds[_items.Selected], Guid.NewGuid().ToString("N"),
            (ItemRarity)_rarity.GetSelectedId(), (int)_level.Value);
        _loading = false;
        RebuildRows();
    }

    private void RebuildRows() => RebuildRows(null);

    private void RebuildRows(IReadOnlyList<AdminAffixChoice> restored)
    {
        if (_loading || _source is null)
        {
            return;
        }

        var previous = restored ?? _rows.GetChildren()
            .OfType<AdminAffixRow>()
            .Select(row => row.Selection)
            .ToArray();

        foreach (var child in _rows.GetChildren())
        {
            _rows.RemoveChild(child);
            child.QueueFree();
        }

        var families = AdminItemCatalog.Families(_source.Base, (int)_level.Value);
        var capacity = ItemRuleContent.LoadRarityRules().GetCapacity((ItemRarity)_rarity.GetSelectedId());

        for (var index = 0; index < capacity; index++)
        {
            var row = new AdminAffixRow { Name = $"Affix{index}", CustomMinimumSize = new(640, 36) };
            _rows.AddChild(row);
            row.Configure(families, (int)_level.Value, index < previous.Count ? previous[index] : null);
        }

        _apply.Disabled = false;
    }

    private void Apply()
    {
        if (_source is null)
        {
            return;
        }

        try
        {
            var choices = _rows.GetChildren()
                .OfType<AdminAffixRow>()
                .Select(row => row.Selection)
                .Where(choice => choice is not null)
                .ToArray();
            var item = AdminItemCatalog.Build(_source, (ItemRarity)_rarity.GetSelectedId(), (int)_level.Value, choices);
            var hud = _arena.GetNode<ArenaHud>("Presentation");

            if (EditExisting)
            {
                if (!_runtime.CombatState.TryEditOwnedItem(_arena.Player.CombatantId, _expected, item))
                {
                    StatusChanged?.Invoke(L.Text("Item changed or is no longer owned. Reload it before saving."));
                    return;
                }

                _expected = _source = item;
                hud.RecordAction(L.Format($"Admin: edited {ItemPresentation.Name(item)}."));
                StatusChanged?.Invoke(L.Text("Item saved. Equipped stats updated."));
                LoadOwnedItem(item.Id);
            }
            else
            {
                var acquired = hud.AcquireItem(item, recordAction: false) == ItemAcquisitionStatus.Acquired;
                var placed = !acquired && _arena.PlaceAdminItem(item);
                hud.RecordAction(L.Format($"Admin: created {ItemPresentation.Name(item)}."));
                StatusChanged?.Invoke(acquired ? L.Text("Item created in inventory.") : placed ? L.Text("Item created on the ground.")
                    : L.Text("Item created; waiting for free ground nearby."));
                _source = AdminItemCatalog.Empty(_source.Base.Id, Guid.NewGuid().ToString("N"),
                    (ItemRarity)_rarity.GetSelectedId(), (int)_level.Value);
            }
        }
        catch (Exception error) when (error is ArgumentException or InvalidOperationException or OverflowException)
        {
            StatusChanged?.Invoke(L.Text("Choose compatible, distinct affixes within the rarity limit and item level."));
        }
    }
}

internal partial class AdminAffixRow : HBoxContainer
{
    internal AdminAffixChoice Selection => _families.Selected <= 0 ? null : new(
        _choices[_families.Selected - 1].GetProperty("id").GetString(),
        _tiers.GetItemText(_tiers.Selected), (decimal)_quality.Value, _original);

    private OptionButton _families;
    private OptionButton _tiers;
    private SpinBox _quality;
    private JsonElement[] _choices = [];
    private int _level;
    private AffixInstance _original;

    internal void Configure(JsonElement[] choices, int level, AdminAffixChoice restored)
    {
        _choices = choices;
        _level = level;
        _original = restored?.Original;
        _families = new OptionButton { Name = "Family", CustomMinimumSize = new(382, 36), FitToLongestItem = false };
        _families.AddThemeFontSizeOverride("font_size", 14);
        _families.AddItem(L.Text("None"));

        foreach (var family in choices)
        {
            _families.AddItem(L.Text(family.GetProperty("name_en").GetString()));
        }

        _tiers = new OptionButton { Name = "Tier", CustomMinimumSize = new(124, 36) };
        _quality = new SpinBox { Name = "Quality", MinValue = 1, MaxValue = 100, Step = 1, Value = 100, CustomMinimumSize = new(126, 36) };
        AddChild(_families);
        AddChild(_tiers);
        AddChild(_quality);
        _families.ItemSelected += _ => RefreshTiers();

        if (restored is not null)
        {
            var index = Array.FindIndex(choices, value => value.GetProperty("id").GetString() == restored.DefinitionId);
            _families.Select(index + 1);
            _quality.Value = (double)restored.Quality;
        }

        RefreshTiers();

        if (restored is not null)
        {
            for (var index = 0; index < _tiers.ItemCount; index++)
            {
                if (_tiers.GetItemText(index) == restored.TierId)
                {
                    _tiers.Select(index);
                }
            }
        }
    }

    private void RefreshTiers()
    {
        _tiers.Clear();
        _tiers.Disabled = true;
        _quality.Editable = false;

        if (_families.Selected <= 0)
        {
            return;
        }

        foreach (var tier in AdminItemCatalog.Tiers(_choices[_families.Selected - 1], _level))
        {
            _tiers.AddItem(tier.GetProperty("tier_id").GetString());
        }

        _tiers.Disabled = false;
        _quality.Editable = true;
    }
}
