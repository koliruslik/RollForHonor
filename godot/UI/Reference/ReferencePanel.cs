using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Godot;
using RollForHonor.Godot.UI.Common;
using RollForHonor.Godot.UI.Localization;

namespace RollForHonor.Godot.UI.Reference;

/// <summary>Provides a compact searchable reference using the same linked explanations as gameplay inspection.</summary>
public partial class ReferencePanel : Control
{
    /// <summary>Raised when the reference closes while independently pinned cards remain available.</summary>
    public event Action Closed;

    /// <summary>Raised when a topic or related link should open as a normal pinnable explanation card.</summary>
    public event Action<InspectionTopic> InspectionRequested;

    private IReadOnlyList<ReferenceEntry> _entries = [];
    private ReferenceEntry _selected;
    private LineEdit _search;
    private OptionButton _category;
    private VBoxContainer _list;
    private Label _count;
    private Label _title;
    private Label _section;
    private RichTextLabel _body;
    private Button _inspect;
    private string[] _categories = [];

    /// <inheritdoc />
    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Stop;
        var panel = UiLayout.Panel(this, "Book", new Rect2(0, 0, 1000, 900));
        BuildHeader(panel);
        BuildIndex(panel);
        BuildReader(panel);
        L.OnChanged(this, () =>
        {
            _search.PlaceholderText = L.Text("Search rules, stats, items…");

            if (Visible)
            {
                Open(_entries);
            }
        });
        Hide();
    }

    /// <summary>Opens a fresh captured catalog, preserving the current search, category and selected identity where possible.</summary>
    /// <param name="entries">The current rules and optional session entries.</param>
    /// <exception cref="ArgumentNullException">The catalog is null.</exception>
    public void Open(IReadOnlyList<ReferenceEntry> entries)
    {
        ArgumentNullException.ThrowIfNull(entries);

        _entries = entries;
        var previous = _category.Selected > 0 ? L.Refresh(_categories[_category.Selected - 1]) : "";
        _category.Clear();
        _category.AddItem(L.Text("All sections"));
        _categories = entries
            .Select(entry => entry.Category)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();

        foreach (var category in _categories)
        {
            _category.AddItem(category);

            if (category == previous)
            {
                _category.Select(_category.ItemCount - 1);
            }
        }

        Filter();
        Show();
        _search.GrabFocus();
    }

    /// <summary>Closes the reader and releases search focus without changing independent inspection cards.</summary>
    public void Close()
    {
        _search.ReleaseFocus();
        Hide();
        Closed?.Invoke();
    }

    private void BuildHeader(Control panel)
    {
        UiLayout.Place(panel, UiLayout.Text(L.Text("Reference"), 28, heading: true), new Rect2(28, 20, 800, 48));
        UiLayout.Place(panel, UiLayout.CloseButton(Close), new Rect2(932, 24, 40, 40));
        UiLayout.Divider(panel, new Rect2(28, 88, 944, 1));
        _search = UiLayout.Place(panel, new LineEdit { Name = "Search", PlaceholderText = L.Text("Search rules, stats, items…"), ClearButtonEnabled = true }, new Rect2(28, 112, 604, 44));
        _search.TextChanged += _ => Filter();
        _category = UiLayout.Place(panel, new OptionButton { Name = "Category" }, new Rect2(648, 112, 324, 44));
        _category.AddItem(L.Text("All sections"));
        _category.ItemSelected += _ => Filter();
    }

    private void BuildIndex(Control panel)
    {
        _count = UiLayout.Place(panel, UiLayout.Text("", 13, HonorTheme.Muted), new Rect2(28, 178, 292, 28));
        var scroll = UiLayout.Place(panel, new ScrollContainer { HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled }, new Rect2(28, 220, 292, 598));
        _list = new VBoxContainer { Name = "Entries", SizeFlagsHorizontal = SizeFlags.ExpandFill };
        _list.AddThemeConstantOverride("separation", 4);
        scroll.AddChild(_list);
        UiLayout.Divider(panel, new Rect2(340, 178, 1, 640));
    }

    private void BuildReader(Control panel)
    {
        _section = UiLayout.Place(panel, UiLayout.Text("", 13, HonorTheme.Brass), new Rect2(368, 178, 604, 28));
        _title = UiLayout.Place(panel, UiLayout.Text("", 24, heading: true), new Rect2(368, 212, 604, 64));
        _title.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        _body = UiLayout.Place(panel, new RichTextLabel { Name = "Article", MetaUnderlined = true }, new Rect2(368, 292, 604, 454));
        _body.AddThemeFontSizeOverride("normal_font_size", 18);
        _body.MetaClicked += FollowLink;
        _inspect = UiLayout.Place(panel, UiLayout.Button(L.Text("Open as card"), () => InspectionRequested?.Invoke(_selected.Topic)), new Rect2(368, 766, 236, 48));
        _inspect.Name = "Inspect";
        UiLayout.Place(panel, UiLayout.Text(L.Text("Search · Select a topic · Underlined text opens related cards · Pin to retain"), 12, new Color(HonorTheme.Muted, 0.6f)), new Rect2(28, 846, 944, 28));
    }

    private void Filter()
    {
        foreach (var child in _list.GetChildren())
        {
            _list.RemoveChild(child);
            child.QueueFree();
        }

        var category = _category.GetItemText(_category.Selected);
        var visible = _entries
            .Where(entry => (_category.Selected == 0 || entry.Category == category) && entry.Matches(_search.Text))
            .ToArray();
        L.SetText(_count, L.Format($"{visible.Length} / {_entries.Count} TOPICS"));

        foreach (var entry in visible)
        {
            AddEntry(entry);
        }

        Select(visible.FirstOrDefault(entry => entry.Id == _selected?.Id) ?? visible.FirstOrDefault());
    }

    private void AddEntry(ReferenceEntry entry)
    {
        var button = UiLayout.Button(entry.Topic.Title, () => Select(entry));
        button.Name = $"Entry{_list.GetChildCount()}";
        button.CustomMinimumSize = new Vector2(272, 46);
        button.Alignment = HorizontalAlignment.Left;
        button.ClipText = true;
        button.TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis;
        button.AddThemeFontSizeOverride("font_size", 16);
        _list.AddChild(button);
        button.SetMeta("reference_id", entry.Id);
    }

    private void Select(ReferenceEntry entry)
    {
        _selected = entry;
        L.SetText(_section, entry?.Category ?? L.Text("SEARCH"));
        L.SetText(_title, entry?.Topic.Title ?? L.Text("No matching topics"));
        _inspect.Disabled = entry is null;
        _body.ScrollToLine(0);

        var buttons = _list.GetChildren()
            .OfType<Button>();

        foreach (var button in buttons)
        {
            button.AddThemeColorOverride("font_color", button.GetMeta("reference_id").AsString() == entry?.Id ? HonorTheme.Brass : HonorTheme.Parchment);
        }

        if (entry is null)
        {
            _body.Clear();
            _body.AddText(L.Text("Try another search or choose All sections."));
            return;
        }

        InspectionContent.Write(_body, entry.Topic);
    }

    private void FollowLink(Variant metadata)
    {
        if (_selected is not null && int.TryParse(metadata.AsString(), NumberStyles.None, CultureInfo.InvariantCulture, out var index)
            && index >= 0 && index < _selected.Topic.Links.Count)
        {
            InspectionRequested?.Invoke(_selected.Topic.Links[index].Open());
        }
    }
}
