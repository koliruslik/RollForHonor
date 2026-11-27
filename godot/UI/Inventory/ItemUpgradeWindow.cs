using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using RollForHonor.Application.Items.Upgrades;
using RollForHonor.Domain.Combat.State.Models;
using RollForHonor.Domain.Items.Affixes.Models;
using RollForHonor.Domain.Items.Configuration;
using RollForHonor.Domain.Items.Models;
using RollForHonor.Domain.Items.Services;
using RollForHonor.Godot.Content.Items;
using RollForHonor.Godot.UI.Common;
using RollForHonor.Godot.UI.Localization;
using RollForHonor.Infrastructure.Combat;
using RollForHonor.Infrastructure.Dice;

namespace RollForHonor.Godot.UI.Inventory;

/// <summary>Edits paid progression and affixes directly within the selected item's properties.</summary>
public partial class ItemUpgradeWindow : Panel
{
    /// <summary>Raised after one successfully paid item change.</summary>
    public event Action<string> Applied;

    private InMemoryCombatState _state;
    private CombatantId _owner;
    private ItemUpgradeService _service;
    private ItemCell _drop;
    private Label _slotHint;
    private Button _clear;
    private Label _wallet;
    private Label _progression;
    private Label _affixHeading;
    private Label _status;
    private ScrollContainer _properties;
    private VBoxContainer _baseRows;
    private VBoxContainer _affixRows;
    private Button _enhance;
    private Button _evolve;
    private Button _reroll;
    private readonly Dictionary<Button, ItemUpgradeQuote> _actions = [];
    private readonly HashSet<string> _locked = new(StringComparer.Ordinal);
    private string _selectedId;
    private ItemInstance _selected;
    private long _version = -1;
    private decimal _luck;
    private string _failure;
    private bool _detailed;

    /// <inheritdoc />
    public override void _Ready()
    {
        Size = new(780, 820);
        AddThemeStyleboxOverride("panel", HonorTheme.Panel(HonorTheme.Ink, HonorTheme.Brass));
        UiLayout.Place(this, UiLayout.Text(L.Text("ITEM UPGRADES"), 22, HonorTheme.Brass, true), new Rect2(20, 14, 640, 38));
        UiLayout.Place(this, UiLayout.CloseButton(Hide), new Rect2(720, 10, 40, 40));
        _drop = UiLayout.Place(this, new ItemCell
        {
            Name = "UpgradeDrop",
            CanReceive = id => _state?.ReadEquipment(_owner).OwnedItems.ContainsKey(id) == true,
            Receive = SelectItem
        }, new Rect2(20, 64, 96, 112));
        _slotHint = UiLayout.Place(this, UiLayout.Text("", 20, heading: true), new Rect2(136, 64, 440, 60));
        _slotHint.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        _clear = UiLayout.Place(this, UiLayout.Button(L.Text("Clear slot"), ClearSelection), new Rect2(600, 64, 160, 40));
        _clear.Name = "ClearSlot";
        _wallet = UiLayout.Place(this, UiLayout.Text("", 16, HonorTheme.Brass), new Rect2(136, 128, 620, 48));
        _properties = UiLayout.Place(this, new ScrollContainer
        {
            Name = "ItemProperties",
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled
        }, new Rect2(20, 196, 740, 536));
        var content = new VBoxContainer { Name = "Content", SizeFlagsHorizontal = SizeFlags.ExpandFill };
        content.AddThemeConstantOverride("separation", 10);
        _properties.AddChild(content);
        content.AddChild(UiLayout.Text(L.Text("BASE PROPERTIES"), 14, HonorTheme.Muted));
        _progression = UiLayout.Text("", 16, HonorTheme.Brass);
        content.AddChild(_progression);
        _baseRows = new VBoxContainer { Name = "BaseProperties", SizeFlagsHorizontal = SizeFlags.ExpandFill };
        content.AddChild(_baseRows);
        _enhance = ActionButton("Enhance", ItemUpgradeOperation.Enhance);
        content.AddChild(_enhance);
        _evolve = ActionButton("Evolve", ItemUpgradeOperation.Evolve);
        content.AddChild(_evolve);
        _affixHeading = UiLayout.Text("", 14, HonorTheme.Muted);
        content.AddChild(_affixHeading);
        _affixRows = new VBoxContainer { Name = "AffixRows", SizeFlagsHorizontal = SizeFlags.ExpandFill };
        _affixRows.AddThemeConstantOverride("separation", 6);
        content.AddChild(_affixRows);
        _reroll = ActionButton("Reroll", ItemUpgradeOperation.Reroll);
        content.AddChild(_reroll);
        _status = UiLayout.Place(this, UiLayout.Text("", 14, HonorTheme.Muted), new Rect2(20, 752, 740, 56));
        _status.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        L.OnChanged(this, Rebuild);
        Hide();
    }

    /// <summary>Composes crafting against the existing scene-local session.</summary>
    /// <param name="state">The canonical item and wallet owner.</param>
    /// <param name="owner">The player actor identity.</param>
    public void Configure(InMemoryCombatState state, CombatantId owner)
    {
        _state = state;
        _owner = owner;
        _service = new(state, ItemUpgradeContent.Rules, new ItemFactory(ItemRuleContent.LoadRarityRules(), ItemStatRules.Default),
            new ItemUpgradeContent(), () => new RandomDiceRoller());
        Rebuild();
    }

    /// <summary>Opens with an empty slot, discarding only the previous selection and unpaid quotes.</summary>
    public void Open()
    {
        ClearSelection();
        Show();
    }

    /// <summary>Selects a dragged owned item without moving ownership or unequipping it.</summary>
    /// <param name="id">An owned inventory or equipped identity.</param>
    public void SelectItem(string id)
    {
        if (_state is null || !_state.ReadEquipment(_owner).OwnedItems.TryGetValue(id, out var item))
        {
            return;
        }

        _selectedId = id;
        _selected = item;
        _locked.Clear();
        _version = -1;
        _properties.ScrollVertical = 0;
        Rebuild();
    }

    /// <inheritdoc />
    public override void _Process(double delta)
    {
        if (!Visible || _state is null)
        {
            return;
        }

        var detailed = Input.IsActionPressed("rfh_details");
        if (_detailed != detailed)
        {
            _detailed = detailed;
            Rebuild();
        }

        var equipment = _state.ReadEquipment(_owner);
        if (_version != equipment.Version)
        {
            _version = equipment.Version;
            var current = _selectedId is not null ? equipment.OwnedItems.GetValueOrDefault(_selectedId) : null;
            if (!ReferenceEquals(current, _selected))
            {
                _selected = current;
                _selectedId = current?.Id;
                _locked.Clear();
                Rebuild();
            }
            else if (_selected is not null && _state.ReadUpgrade(_owner, _selected.Id, ItemUpgradeContent.Rules.ResourceId).Luck != _luck)
            {
                Rebuild();
            }
        }

        RefreshAdmission();
    }

    private Button ActionButton(string name, ItemUpgradeOperation operation)
    {
        var button = UiLayout.Button("", () => Execute(operation), true);
        button.Name = name;
        button.CustomMinimumSize = new(0, 42);
        return button;
    }

    private void ClearSelection()
    {
        _selectedId = null;
        _selected = null;
        _locked.Clear();
        _version = -1;
        Rebuild();
    }

    private void Rebuild()
    {
        if (_service is null || _enhance is null)
        {
            return;
        }

        _failure = null;
        _actions.Clear();
        ClearRows(_baseRows);
        ClearRows(_affixRows);
        _slotHint.Text = _selected is null ? L.Text("Drag an item here from inventory or equipment.") : ItemPresentation.Name(_selected);
        _clear.Visible = _properties.Visible = _selected is not null;
        _enhance.Disabled = _evolve.Disabled = _reroll.Disabled = true;
        if (_selected is null)
        {
            _drop.ItemId = "";
            _drop.SetAppearance("bag", HonorTheme.Border, false);
            _evolve.Hide();
            RefreshAdmission();
            return;
        }

        var equipment = _state.ReadEquipment(_owner);
        _drop.ItemId = _selected.Id;
        _drop.SetAppearance(ItemPresentation.Icon(_selected, ItemPresentation.Slot(_selected, equipment.Rules)),
            ItemPresentation.RarityColor(_selected.Rarity), true, _selected.Progression.Enhancement);
        var enhance = Quote(ItemUpgradeOperation.Enhance);
        var evolve = Quote(ItemUpgradeOperation.Evolve);
        _luck = enhance.Luck;
        _actions[_enhance] = enhance;
        _actions[_evolve] = evolve;
        _actions[_reroll] = Quote(ItemUpgradeOperation.Reroll);
        var limit = ItemUpgradeContent.Rules.EnhancementLimit(_selected.Rarity);
        _progression.Text = L.Format($"{_selected.Rarity} · Enhancement +{_selected.Progression.Enhancement}/{limit}");
        _evolve.Visible = _selected.Rarity != ItemRarity.Legendary && _selected.Progression.Enhancement >= limit;
        BuildBaseProperties(enhance.Result ?? evolve.Result);
        var capacity = ItemRuleContent.LoadRarityRules().GetCapacity(_selected.Rarity);
        _affixHeading.Text = L.Format($"AFFIXES · {_selected.Affixes.Count}/{capacity}");
        foreach (var affix in _selected.Affixes)
        {
            AddAffixRow(affix);
        }

        if (_selected.Affixes.Count < capacity)
        {
            var add = Quote(ItemUpgradeOperation.AddAffix);
            for (var index = _selected.Affixes.Count; index < capacity; index++)
            {
                var button = ActionButton($"AddAffix{index}", ItemUpgradeOperation.AddAffix);
                _affixRows.AddChild(button);
                _actions[button] = add;
            }
        }

        RefreshAdmission();
    }

    private void BuildBaseProperties(ItemInstance next)
    {
        if (_selected.Base.Weapon is { } weapon)
        {
            PropertyLine(L.Format($"Attack speed  {weapon.AttacksPerSecond:0.##}/s"));
            if (weapon.ManaCost > 0)
            {
                PropertyLine(L.Format($"Mana cost  {weapon.ManaCost:0.##}"));
            }

            var before = ItemBaseScaling.WeaponDamage(_selected);
            var after = next is null ? before : ItemBaseScaling.WeaponDamage(next);
            for (var index = 0; index < before.Count; index++)
            {
                var currentDamage = _detailed ? ItemPresentation.WeaponDamageFormula(before[index], includeType: false)
                    : ItemPresentation.WeaponDamageRange(before[index], includeType: false);
                var targetDamage = _detailed ? ItemPresentation.WeaponDamageFormula(after[index], includeType: false)
                    : ItemPresentation.WeaponDamageRange(after[index], includeType: false);
                PropertyLine(_detailed ? ItemPresentation.WeaponDamageFormula(before[index]) : ItemPresentation.WeaponDamageRange(before[index]),
                    currentDamage == targetDamage ? null : targetDamage);
            }
        }

        var current = ItemBaseScaling.Modifiers(_selected).ToArray();
        var target = next is null ? current : ItemBaseScaling.Modifiers(next).ToArray();
        for (var index = 0; index < current.Length; index++)
        {
            PropertyLine(ItemPresentation.CompactModifier(current[index]), current[index] == target[index]
                ? null : ItemPresentation.CompactModifier(target[index]));
        }
    }

    private void PropertyLine(string value, string next = null)
    {
        var line = new RichTextLabel
        {
            FitContent = true,
            ScrollActive = false,
            CustomMinimumSize = new(0, 25),
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            MouseFilter = MouseFilterEnum.Ignore
        };
        line.AddThemeFontSizeOverride("normal_font_size", 16);
        _baseRows.AddChild(line);
        line.AddText(value);
        if (next is not null)
        {
            line.PushColor(new Color("8fb697"));
            line.AddText("  →  " + next);
            line.Pop();
        }
    }

    private void AddAffixRow(AffixInstance affix)
    {
        var row = new HBoxContainer { CustomMinimumSize = new(0, 38), SizeFlagsHorizontal = SizeFlags.ExpandFill };
        var toggle = new Button
        {
            Name = "Lock",
            ToggleMode = true,
            ButtonPressed = _locked.Contains(affix.Id),
            CustomMinimumSize = new(36, 36),
            Flat = true,
            TooltipText = L.Text("Lock affix")
        };
        row.AddChild(toggle);
        var icon = UiLayout.Place(toggle, UiLayout.Icon("affix-lock"), new Rect2(2, 2, 32, 32));
        icon.Name = "LockIcon";
        icon.Modulate = _locked.Contains(affix.Id) ? HonorTheme.Brass : new Color(1, 1, 1, 0.2f);
        var text = string.Join("\n", affix.Modifiers
            .Take(ItemInspectionTopics.GroupLabel(affix) is null ? affix.Modifiers.Count : 1)
            .Select((modifier, index) => ItemInspectionTopics.FormatModifier(affix, modifier, index, false)));
        var label = UiLayout.Text(text, 16);
        label.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        label.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        row.AddChild(label);
        toggle.Toggled += value =>
        {
            if (value)
            {
                _locked.Add(affix.Id);
            }
            else
            {
                _locked.Remove(affix.Id);
            }
            Rebuild();
        };
        _affixRows.AddChild(row);
    }

    private ItemUpgradeQuote Quote(ItemUpgradeOperation operation) => _service.Quote(_owner, _selected, operation,
        operation == ItemUpgradeOperation.Reroll ? _locked : []);

    private void RefreshAdmission()
    {
        if (_state is null)
        {
            return;
        }

        var balance = _state.ReadGoldWallet(_owner).Balance;
        _wallet.Text = L.Format($"Gold: {balance}");
        var read = _selected is null ? null : _state.ReadUpgrade(_owner, _selected.Id, ItemUpgradeContent.Rules.ResourceId);
        foreach (var pair in _actions)
        {
            var quote = pair.Value;
            var status = quote.Status;
            if (status == ItemUpgradeStatus.Ready)
            {
                status = read.Defeated ? ItemUpgradeStatus.Defeated
                    : !ReferenceEquals(read.Item, quote.Item) || read.Luck != quote.Luck ? ItemUpgradeStatus.Stale
                    : balance < quote.Cost.Amount ? ItemUpgradeStatus.InsufficientFunds : ItemUpgradeStatus.Ready;
            }

            pair.Key.Disabled = status != ItemUpgradeStatus.Ready || GetTree().Paused;
            var caption = OperationName(quote.Operation);
            if (quote.Result is { } next && quote.Operation is ItemUpgradeOperation.Enhance or ItemUpgradeOperation.Evolve)
            {
                caption = quote.Operation == ItemUpgradeOperation.Evolve
                    ? L.Format($"{caption} → {next.Rarity} +{next.Progression.Enhancement}")
                    : L.Format($"{caption} → +{next.Progression.Enhancement}");
            }
            pair.Key.Text = quote.Cost.Amount > 0 ? L.Format($"{caption} · {quote.Cost.Amount} gold") : caption;
            pair.Key.TooltipText = StatusText(status);
        }

        _status.Text = _selected is null ? L.Text("Drag an item into the empty slot to upgrade it.") : _failure
            ?? L.Text("Green values show the next upgrade. Click an empty affix slot to fill it.");
    }

    private void Execute(ItemUpgradeOperation operation)
    {
        var action = _actions.FirstOrDefault(pair => pair.Value.Operation == operation);
        if (action.Key is null || action.Key.Disabled || GetTree().Paused)
        {
            return;
        }

        try
        {
            var status = _service.Execute(action.Value);
            if (status == ItemUpgradeStatus.Applied)
            {
                _selected = action.Value.Result;
                _locked.IntersectWith(_selected.Affixes.Select(affix => affix.Id));
                _version = -1;
                Rebuild();
                Applied?.Invoke(L.Format($"Upgraded: {ItemPresentation.Name(_selected)}"));
            }
            else
            {
                _failure = StatusText(status);
                RefreshAdmission();
            }
        }
        catch (Exception error) when (error is ArgumentException or InvalidOperationException or OverflowException)
        {
            _failure = L.Text("Upgrade could not be applied. No gold was spent.");
            RefreshAdmission();
        }
    }

    private static void ClearRows(Node rows)
    {
        foreach (var child in rows.GetChildren())
        {
            rows.RemoveChild(child);
            child.QueueFree();
        }
    }

    private static string OperationName(ItemUpgradeOperation operation) => L.Text(operation switch
    {
        ItemUpgradeOperation.Enhance => "Enhance",
        ItemUpgradeOperation.Evolve => "Evolve",
        ItemUpgradeOperation.Reroll => "Reroll unlocked affixes",
        _ => "+ Empty affix slot"
    });

    private static string StatusText(ItemUpgradeStatus status) => L.Text(status switch
    {
        ItemUpgradeStatus.Ready => "Guaranteed result. Gold is spent only on success.",
        ItemUpgradeStatus.InsufficientFunds => "Not enough gold.",
        ItemUpgradeStatus.Stale => "Item or Luck changed. Select the item again.",
        ItemUpgradeStatus.Defeated => "A defeated character cannot upgrade items.",
        ItemUpgradeStatus.UnsupportedBase => "Upgrade prices are not configured for this generation.",
        ItemUpgradeStatus.NoEligibleAffixes => "No compatible affix pool for this item and level.",
        _ => "Rarity limit reached, no free slots or no unlocked affixes."
    });
}
