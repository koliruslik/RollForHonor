using System;
using System.Collections.Generic;
using Godot;
using RollForHonor.Domain.Items.Equipment.Models;
using RollForHonor.Domain.Items.Models;
using RollForHonor.Godot.UI.Common;
using RollForHonor.Godot.Gameplay.Input;
using RollForHonor.Godot.UI.Localization;

namespace RollForHonor.Godot.UI.Inventory;

/// <summary>Fits intrinsic properties, affix contributions and optional range/quality details into a content-sized card.</summary>
public partial class ItemDetailsPanel : Control
{
    /// <summary>Shared card width used by pinned, comparison and hover layouts.</summary>
    public const float CardWidth = 400;

    /// <summary>Raised when this pinned card is dismissed.</summary>
    public event Action Closed;

    /// <summary>Raised when the selected owned item is discarded into the world.</summary>
    public event Action DropRequested;

    /// <summary>Raised when the pointer inspects affixes contributing to a highlighted property.</summary>
    public event Action<string> AffixHovered;

    /// <summary>Raised when underlined item text opens a stationary explanation.</summary>
    public event Action<InspectionTopic> InspectionRequested;

    /// <summary>Gets whether Alt is holding the cursor tooltip in place for interaction.</summary>
    public bool IsInspecting => _hover && _detailed;

    private Panel _panel;
    private Panel _header;
    private Label _caption;
    private Label _name;
    private Label _rarity;
    private TextureRect _icon;
    private RichTextLabel _properties;
    private Button _action;
    private Button _close;
    private Button _discard;
    private Label _status;
    private Action _activate;
    private ItemInstance _item;
    private bool _detailed;
    private bool _hover;
    private readonly Dictionary<string, string> _sources = new();
    private readonly Dictionary<string, InspectionTopic> _inspections = new();

    /// <inheritdoc />
    public override void _Ready()
    {
        L.OnChanged(this, () =>
        {
            if (_item is not null)
            {
                BuildProperties();
                RefreshLayout();
            }
        });

        MouseFilter = MouseFilterEnum.Stop;
        _panel = UiLayout.Panel(this, "Card", new Rect2(0, 0, 400, 680));
        Size = _panel.Size;
        _header = UiLayout.Panel(_panel, "Header", new Rect2(2, 2, 396, 160));
        _caption = UiLayout.Place(_header, UiLayout.Text(L.Text("SELECTED ITEM"), 12, HonorTheme.Muted), new Rect2(16, 8, 320, 28));
        _close = UiLayout.Place(_header, UiLayout.CloseButton(() => Closed?.Invoke()), new Rect2(342, 8, 40, 40));
        _icon = UiLayout.Place(_header, UiLayout.Icon("bag"), new Rect2(16, 44, 48, 48));
        _rarity = UiLayout.Place(_header, UiLayout.Text("", 14), new Rect2(80, 44, 300, 48));
        _rarity.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        _name = UiLayout.Place(_header, UiLayout.Text("", 21, heading: true), new Rect2(16, 96, 364, 56));
        _name.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        _properties = UiLayout.Place(_panel, new RichTextLabel { Name = "Properties", MouseFilter = MouseFilterEnum.Stop, BbcodeEnabled = true, MetaUnderlined = true }, new Rect2(20, 180, 360, 344));
        _properties.AddThemeFontSizeOverride("normal_font_size", 15);
        _properties.AddThemeConstantOverride("line_separation", 1);
        _properties.MetaHoverStarted += key => AffixHovered?.Invoke(_sources.GetValueOrDefault(key.AsString(), ""));
        _properties.MetaHoverEnded += _ => AffixHovered?.Invoke("");
        _properties.MetaClicked += key =>
        {
            if (_inspections.TryGetValue(key.AsString(), out var topic))
            {
                InspectionRequested?.Invoke(topic);
            }
        };
        _action = UiLayout.Place(_panel, UiLayout.Button(L.Text("Equip"), () => _activate?.Invoke(), true), new Rect2(20, 536, 360, 48));
        _action.Name = "Action";
        _discard = UiLayout.Place(_panel, UiLayout.Button(L.Text("Drop item"), () => DropRequested?.Invoke()), new Rect2(20, 590, 360, 44));
        _discard.Name = "Discard";
        _discard.Hide();
        _status = UiLayout.Place(_panel, UiLayout.Text(L.Text("Hold Alt for ranges and current tier"), 12, new Color(HonorTheme.Muted, 0.55f)), new Rect2(20, 604, 360, 52));
        _status.Name = "Status";
        _status.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        _status.VerticalAlignment = VerticalAlignment.Top;
        Hide();
    }

    /// <inheritdoc />
    public override void _Process(double delta)
    {
        if (!Visible)
        {
            return;
        }

        RefreshLayout();

        if (_hover)
        {
            var parent = GetParent<Control>();
            var cursor = parent.GetLocalMousePosition();
            Position = _detailed ? Position.Clamp(Vector2.Zero, (parent.Size - Size).Max(Vector2.Zero))
                : TooltipPlacement.Place(parent.Size, Size, cursor, TooltipPlacement.SourceBounds(parent, cursor));
        }
    }

    /// <summary>Displays real properties of a captured owned item.</summary>
    /// <param name="item">The captured item.</param>
    /// <param name="slot">Its compatible or occupied slot, or null for non-equipment.</param>
    /// <param name="equipped">Whether the item is equipped.</param>
    /// <param name="activate">The operation invoked by the action button.</param>
    public void ShowItem(ItemInstance item, EquipmentSlot? slot, bool equipped, Action activate)
    {
        if (!ReferenceEquals(_item, item))
        {
            ResetStatus();
        }

        _item = item;
        var color = ItemPresentation.RarityColor(item.Rarity);
        _panel.AddThemeStyleboxOverride("panel", HonorTheme.Panel(new Color(HonorTheme.Ink.Lerp(color.Darkened(0.8f), 0.3f), _hover ? 0.86f : 1), color, 0));
        _header.AddThemeStyleboxOverride("panel", HonorTheme.Panel(new Color(HonorTheme.Ink.Lerp(color.Darkened(0.55f), 0.4f), _hover ? 0.86f : 1), color, 0));
        L.SetText(_caption, _hover ? L.Text("ITEM") : L.Text("SELECTED ITEM"));
        L.SetText(_name, ItemPresentation.Name(item));
        _name.AddThemeColorOverride("font_color", color.Lightened(0.15f));
        var slotName = slot is { } target ? ItemPresentation.SlotName(target) : L.Text("Item");
        L.SetText(_rarity, L.Format($"{item.Rarity}\n{slotName}"));
        _rarity.AddThemeColorOverride("font_color", color);
        _icon.Texture = GD.Load<Texture2D>($"res://UI/Assets/{ItemPresentation.Icon(item, slot)}.svg");
        _action.Visible = !_hover;
        _discard.Visible = !_hover && DropRequested is not null;
        _action.Disabled = slot is null;
        L.SetText(_action, slot is null ? L.Text("Cannot equip") : equipped ? L.Format($"Unequip · {slotName}") : L.Format($"Equip · {slotName}"));
        _activate = activate;
        BuildProperties();
        Show();
        RefreshLayout();
    }

    /// <summary>Shows the actual item being replaced without offering an action on the comparison card.</summary>
    /// <param name="item">The currently equipped item.</param>
    /// <param name="slot">Its occupied slot.</param>
    public void ShowComparison(ItemInstance item, EquipmentSlot slot)
    {
        ShowItem(item, slot, true, null);
        L.SetText(_caption, L.Text("CURRENTLY EQUIPPED"));
        _action.Hide();
        _discard.Hide();
        RefreshLayout();
    }

    /// <summary>Controls whether affixes show captured quality, ranges and their current tier.</summary>
    /// <param name="detailed">True while the details modifier is held.</param>
    public void SetDetailed(bool detailed)
    {
        if (_detailed != detailed)
        {
            _detailed = detailed;

            if (_hover)
            {
                SetHoverInteractivity();
            }

            if (_item is not null)
            {
                BuildProperties();
                RefreshLayout();
            }
        }
    }

    /// <summary>Configures this instance as a noninteractive cursor-following tooltip.</summary>
    public void UseAsHover()
    {
        _hover = true;
        SetHoverInteractivity();
        _close.Hide();
        _action.Hide();
    }

    /// <summary>Shows an equipment-operation result.</summary>
    /// <param name="message">The short result text.</param>
    public void SetStatus(string message)
    {
        L.SetText(_status, message);
        _status.AddThemeFontSizeOverride("font_size", 14);
        _status.AddThemeColorOverride("font_color", HonorTheme.Secondary);
        RefreshLayout();
    }

    private void RefreshLayout()
    {
        var actionHeight = _action.Visible ? Mathf.Max(48, _action.GetCombinedMinimumSize().Y) : 0;
        var statusHeight = Mathf.Max(24, _status.GetCombinedMinimumSize().Y);
        var footerHeight = 16 + actionHeight + (_action.Visible ? 20 : 0) + (_discard.Visible ? 52 : 0) + statusHeight + 24;
        var contentLimit = Mathf.Max(24, AvailableHeight() - 180 - footerHeight);
        var contentHeight = Mathf.Clamp(_properties.GetContentHeight() + 4, 24, contentLimit);
        _properties.Size = new Vector2(360, contentHeight);
        LayoutFooter(180 + contentHeight + 16, actionHeight, statusHeight);
    }

    private float AvailableHeight()
    {
        var parent = GetParent() as Control;
        var height = parent?.Size.Y ?? GetViewportRect().Size.Y / GetGlobalTransformWithCanvas().Scale.Y;

        return height - (_hover ? 0 : Position.Y) - 16;
    }

    private void LayoutFooter(float top, float actionHeight, float statusHeight)
    {
        if (_action.Visible)
        {
            _action.Position = new Vector2(20, top);
            _action.Size = new Vector2(360, actionHeight);
            top += actionHeight + 20;
        }

        if (_discard.Visible)
        {
            _discard.Position = new Vector2(20, top);
            _discard.Size = new Vector2(360, 44);
            top += 52;
        }

        _status.Position = new Vector2(20, top);
        _status.Size = new Vector2(360, statusHeight);
        Size = _panel.Size = new Vector2(400, top + statusHeight + 24);
    }

    private void ResetStatus()
    {
        L.SetText(_status, L.Format($"{GameInputBindings.Name("rfh_details")}  Ranges / current tier"));
        _status.AddThemeFontSizeOverride("font_size", 12);
        _status.AddThemeColorOverride("font_color", new Color(HonorTheme.Muted, 0.55f));
    }

    private void SetHoverInteractivity()
    {
        MouseFilter = _panel.MouseFilter = _header.MouseFilter = _properties.MouseFilter =
            _detailed ? MouseFilterEnum.Stop : MouseFilterEnum.Ignore;
    }

}
