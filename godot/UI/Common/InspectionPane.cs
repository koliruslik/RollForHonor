using System;
using Godot;

namespace RollForHonor.Godot.UI.Common;

/// <summary>A stationary explanation card whose underlined links open further cards.</summary>
public partial class InspectionPane : Control
{
    /// <summary>Raised when a related concept is selected.</summary>
    public event Action<InspectionTopic> TopicRequested;

    /// <summary>Raised when this card and its descendants should close.</summary>
    public event Action Closed;

    /// <summary>Raised when the card's independent retention state changes.</summary>
    public event Action PinnedChanged;

    /// <summary>Gets whether this card survives closing its source window.</summary>
    public bool IsPinned { get; private set; }

    /// <summary>Gets whether the user has retained or moved the card's current position.</summary>
    public bool HasCustomPosition { get; private set; }

    private Panel _panel;
    private Label _title;
    private RichTextLabel _body;
    private InspectionTopic _topic;
    private Button _pin;
    private Control _dragHandle;
    private bool _dragging;
    private Vector2 _grabOffset;

    /// <inheritdoc />
    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Stop;
        _panel = UiLayout.Panel(this, "Card", new Rect2(0, 0, 360, 240));
        _dragHandle = UiLayout.Place(_panel, new Control { Name = "DragHandle", MouseFilter = MouseFilterEnum.Stop, MouseDefaultCursorShape = CursorShape.Drag }, new Rect2(0, 0, 248, 68));
        _dragHandle.GuiInput += DragHeader;
        _title = UiLayout.Place(_panel, UiLayout.Text("", 20, heading: true), new Rect2(16, 12, 224, 48));
        _title.Name = "Title";
        _title.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        _pin = UiLayout.Place(_panel, UiLayout.CloseButton(TogglePinned), new Rect2(256, 12, 40, 40));
        _pin.Name = "Pin";
        _pin.Text = "";
        _pin.ToggleMode = true;
        _pin.TooltipText = "Pin";
        UiLayout.Place(_pin, UiLayout.Icon("pin"), new Rect2(10, 10, 20, 20));
        UiLayout.Place(_panel, UiLayout.CloseButton(() => Closed?.Invoke()), new Rect2(304, 12, 40, 40));
        UiLayout.Divider(_panel, new Rect2(16, 68, 328, 1));
        _body = UiLayout.Place(_panel, new RichTextLabel { Name = "Body", MetaUnderlined = true, MouseFilter = MouseFilterEnum.Stop }, new Rect2(16, 84, 328, 140));
        _body.AddThemeFontSizeOverride("normal_font_size", 16);
        _body.AddThemeConstantOverride("line_separation", 3);
        _body.MetaClicked += FollowLink;
    }

    /// <inheritdoc />
    public override void _Process(double delta)
    {
        var available = GetParent<Control>().Size.Y - 32;
        var height = Mathf.Clamp(_body.GetContentHeight() + 108, 180, Mathf.Max(180, available));
        Size = _panel.Size = new Vector2(360, height);
        _body.Size = new Vector2(328, height - 108);
        ClampPosition();
    }

    /// <summary>Displays captured text and safe, underlined related links.</summary>
    /// <param name="topic">The explanation to render.</param>
    public void ShowTopic(InspectionTopic topic)
    {
        _topic = topic;
        _title.Text = topic.Title;
        _title.AddThemeColorOverride("font_color", HonorTheme.Parchment);
        _panel.AddThemeStyleboxOverride("panel", HonorTheme.Panel(new Color("141a1d"), topic.Accent.Darkened(0.4f), 0));
        InspectionContent.Write(_body, topic);
    }

    private void DragHeader(InputEvent input)
    {
        if (input is InputEventMouseButton { ButtonIndex: MouseButton.Left } button)
        {
            _dragging = button.Pressed;
            _grabOffset = button.Position;
            HasCustomPosition |= _dragging;

            if (_dragging)
            {
                GetParent().MoveChild(this, -1);
            }

            _dragHandle.AcceptEvent();
        }
        else if (_dragging && input is InputEventMouseMotion motion)
        {
            Position += motion.Position - _grabOffset;
            ClampPosition();
            _dragHandle.AcceptEvent();
        }
    }

    private void TogglePinned()
    {
        IsPinned = !IsPinned;
        HasCustomPosition = true;
        _pin.ButtonPressed = IsPinned;
        _pin.TooltipText = IsPinned ? "Unpin" : "Pin";
        PinnedChanged?.Invoke();
    }

    private void FollowLink(Variant metadata)
    {
        if (int.TryParse(metadata.AsString(), out var index) && index >= 0 && index < _topic.Links.Count)
        {
            TopicRequested?.Invoke(_topic.Links[index].Open());
        }
    }

    private void ClampPosition()
    {
        var limit = (GetParent<Control>().Size - Size - new Vector2(16, 16)).Max(new Vector2(16, 16));
        Position = Position.Clamp(new Vector2(16, 16), limit);
    }
}
