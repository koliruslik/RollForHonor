using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Godot;
using RollForHonor.Godot.UI.Common;
using RollForHonor.Godot.UI.Localization;

namespace RollForHonor.Godot.UI.Reference;

/// <summary>Organizes the bestiary, item and affix tables, rules and session history into separate pages.</summary>
public partial class ReferencePanel : Control
{
    /// <summary>Raised when the reader closes while independent pinned cards remain.</summary>
    public event Action Closed;

    /// <summary>Raised when a rule or table row should open as a pinnable inspection.</summary>
    public event Action<InspectionTopic> InspectionRequested;

    private static readonly (string Id, string Title)[] Sections =
    [
        ("monsters", "Monsters"), ("items", "Items"), ("affixes", "Affixes"),
        ("rules", "Rules"), ("journal", "Journal")
    ];

    private IReadOnlyList<ReferenceEntry> _entries = [];
    private ReferenceEntry _selected;
    private string _sectionId = "monsters";
    private string[] _filters = [];
    private string _filter = "";
    private LineEdit _search;
    private OptionButton _category;
    private OptionButton _tier;
    private int _tierIndex;
    private VBoxContainer _list;
    private ScrollContainer _index;
    private Label _count;
    private Label _title;
    private Label _section;
    private RichTextLabel _body;
    private Button _inspect;
    private ReferenceTableView _table;
    private readonly Dictionary<string, Button> _tabs = new(StringComparer.Ordinal);

    /// <inheritdoc />
    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Stop;
        var panel = UiLayout.Panel(this, "Book", new Rect2(0, 0, 1000, 900));
        BuildHeader(panel);
        BuildContent(panel);
        L.OnChanged(this, () =>
        {
            if (Visible)
            {
                var luck = _entries.Select(entry => entry.CapturedLuck).DefaultIfEmpty().Max();
                var pages = LootReference.Create(luck).ToDictionary(entry => entry.Id, StringComparer.Ordinal);
                _entries = _entries
                    .Select(entry => pages.TryGetValue(entry.Id, out var localized) ? localized : entry)
                    .ToArray();
                Open(_entries);
            }
        });
        Hide();
    }

    /// <summary>Opens captured content in the last used section; first opening shows the bestiary.</summary>
    /// <param name="entries">The current catalog and optional session snapshots.</param>
    /// <exception cref="ArgumentNullException">The catalog is null.</exception>
    public void Open(IReadOnlyList<ReferenceEntry> entries)
    {
        ArgumentNullException.ThrowIfNull(entries);
        _entries = entries;
        RefreshFilters();
        Filter();
        Show();
        _search.GrabFocus();
    }

    /// <summary>Changes section and clears the previous section's search and filters.</summary>
    /// <param name="sectionId">monsters, items, affixes, rules or journal.</param>
    /// <exception cref="ArgumentException">The section identity is unknown.</exception>
    public void SelectSection(string sectionId)
    {
        if (!Sections.Any(section => section.Id == sectionId))
        {
            throw new ArgumentException("Unknown reference section.", nameof(sectionId));
        }

        _sectionId = sectionId;
        _selected = null;
        _search.Text = "";
        _filter = "";
        RefreshFilters();
        Filter();
    }

    /// <summary>Navigates directly to a known entry while preserving normal section ownership.</summary>
    /// <param name="entryId">The stable entry identity.</param>
    /// <exception cref="InvalidOperationException">The entry is missing.</exception>
    public void ShowEntry(string entryId)
    {
        var entry = _entries.Single(value => value.Id == entryId);
        SelectSection(SectionFor(entry));
        Select(entry);
    }

    /// <summary>Closes the reader and releases text focus.</summary>
    public void Close()
    {
        _search.ReleaseFocus();
        Hide();
        Closed?.Invoke();
    }

    private void BuildHeader(Control panel)
    {
        UiLayout.Place(panel, UiLayout.Text(L.Text("Reference"), 28, heading: true), new Rect2(28, 18, 800, 48));
        UiLayout.Place(panel, UiLayout.CloseButton(Close), new Rect2(932, 24, 40, 40));

        for (var index = 0; index < Sections.Length; index++)
        {
            var section = Sections[index];
            var button = UiLayout.Place(panel, UiLayout.Button(L.Text(section.Title), () => SelectSection(section.Id)), new Rect2(28 + index * 190, 78, 184, 42));
            button.Name = $"Section{section.Id}";
            button.AddThemeFontSizeOverride("font_size", 18);
            _tabs.Add(section.Id, button);
        }

        _search = UiLayout.Place(panel, new LineEdit { Name = "Search", ClearButtonEnabled = true }, new Rect2(28, 140, 604, 44));
        _search.TextChanged += _ => Filter();
        _category = UiLayout.Place(panel, new OptionButton { Name = "Category" }, new Rect2(648, 140, 324, 44));
        _category.ItemSelected += index =>
        {
            _filter = index == 0 ? "" : _filters[index - 1];
            Filter();
        };
        _tier = UiLayout.Place(panel, new OptionButton { Name = "Tier", Visible = false }, new Rect2(474, 140, 158, 44));
        _tier.TooltipText = L.Text("Select an affix tier to view its value ranges.");
        _tier.ItemSelected += index =>
        {
            _tierIndex = (int)index;
            Select(_selected);
        };
    }

    private void BuildContent(Control panel)
    {
        _count = UiLayout.Place(panel, UiLayout.Text("", 14, HonorTheme.Muted), new Rect2(28, 204, 230, 30));
        _index = UiLayout.Place(panel, new ScrollContainer { HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled }, new Rect2(28, 246, 230, 574));
        _list = new VBoxContainer { Name = "Entries", SizeFlagsHorizontal = SizeFlags.ExpandFill };
        _list.AddThemeConstantOverride("separation", 6);
        _index.AddChild(_list);
        _section = UiLayout.Place(panel, UiLayout.Text("", 13, HonorTheme.Brass), new Rect2(282, 204, 690, 28));
        _title = UiLayout.Place(panel, UiLayout.Text("", 26, heading: true), new Rect2(282, 240, 690, 48));
        _title.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        _body = UiLayout.Place(panel, new RichTextLabel { Name = "Article", MetaUnderlined = true }, new Rect2(282, 300, 690, 444));
        _body.AddThemeFontSizeOverride("normal_font_size", 18);
        _body.MetaClicked += FollowLink;
        _table = new ReferenceTableView { Name = "Table" };
        panel.AddChild(_table);
        _table.InspectionRequested += topic => InspectionRequested?.Invoke(topic);
        _inspect = UiLayout.Place(panel, UiLayout.Button(L.Text("Open as card"), () => InspectionRequested?.Invoke(_selected.Topic)), new Rect2(282, 774, 236, 46));
        _inspect.Name = "Inspect";
        UiLayout.Place(panel, UiLayout.Text(L.Text("Choose a section. Search within it. Select a monster or a table row for details."), 13, HonorTheme.Muted), new Rect2(28, 846, 944, 36));
    }

    private void RefreshFilters()
    {
        var table = _sectionId == "items" ? _entries.FirstOrDefault(entry => entry.Id == "loot.bases")?.Table
            : _sectionId == "affixes" ? _entries.FirstOrDefault(entry => entry.Id == "loot.affixes")?.Table
            : _selected?.Table;
        _filters = table is not null ? table.Filters.ToArray()
            : _sectionId == "rules" ? _entries
                .Where(entry => SectionFor(entry) == "rules")
                .Select(entry => entry.Category)
                .Distinct(StringComparer.Ordinal)
                .OrderBy(value => value, StringComparer.Ordinal)
                .ToArray() : [];
        _category.Clear();
        _category.AddItem(L.Text(table is not null ? "All rows" : "All topics in this section"));

        foreach (var filter in _filters)
        {
            _category.AddItem(FilterName(filter));
        }

        var selected = Array.FindIndex(_filters, value => L.Refresh(value) == L.Refresh(_filter));
        _category.Select(selected + 1);
        _filter = selected < 0 ? "" : _filters[selected];
        _category.Disabled = _filters.Length == 0;
    }

    private void Filter()
    {
        foreach (var child in _list.GetChildren())
        {
            _list.RemoveChild(child);
            child.QueueFree();
        }

        foreach (var tab in _tabs)
        {
            tab.Value.AddThemeColorOverride("font_color", tab.Key == _sectionId ? HonorTheme.Brass : HonorTheme.Parchment);
        }

        _search.PlaceholderText = L.Text(_sectionId is "items" or "affixes" ? "Search this table…" : "Search this section…");
        var entries = _entries.Where(entry => SectionFor(entry) == _sectionId);
        var tablePage = _sectionId is "items" or "affixes";
        var visible = entries
            .Where(entry => tablePage ? entry.Table is not null
                : (string.IsNullOrEmpty(_filter) || _sectionId != "rules" || entry.Category == _filter) && entry.Matches(_search.Text))
            .ToArray();

        foreach (var entry in visible)
        {
            var button = UiLayout.Button(entry.Topic.Title, () => Select(entry));
            button.CustomMinimumSize = new Vector2(212, 52);
            button.Alignment = HorizontalAlignment.Left;
            button.ClipText = true;
            button.AddThemeFontSizeOverride("font_size", 17);
            button.SetMeta("reference_id", entry.Id);
            _list.AddChild(button);
        }

        L.SetText(_count, L.Format($"{visible.Length} entries"));
        Select(visible.FirstOrDefault(entry => entry.Id == _selected?.Id) ?? visible.FirstOrDefault());
    }

    private void Select(ReferenceEntry entry)
    {
        var changed = entry?.Id != _selected?.Id;
        _selected = entry;
        var wide = _sectionId is "items" or "affixes";
        var left = wide ? 28 : 282;
        var width = wide ? 944 : 690;
        _index.Visible = _count.Visible = !wide;
        _section.Visible = !wide;
        _title.Visible = !wide;
        _body.Visible = !wide;
        _section.Position = new(left, 204);
        _section.Size = new(width, 28);
        _title.Position = new(left, 240);
        _title.Size = new(width, 48);
        _body.Position = new(left, 300);
        _body.Size = new(width, entry?.Table is null ? 444 : wide ? 78 : 156);
        L.SetText(_section, entry?.Category ?? L.Text("Search"));
        L.SetText(_title, entry?.Topic.Title ?? L.Text("No matching topics"));
        _inspect.Visible = entry?.Table is null;
        _inspect.Disabled = entry is null;
        _inspect.Position = new(left, 774);
        _table.Visible = entry?.Table is not null;
        _tier.Visible = false;
        _search.Size = new(604, 44);
        _body.ScrollToLine(0);

        if (changed && _sectionId == "monsters")
        {
            _filter = "";
            RefreshFilters();
        }

        if (entry is null)
        {
            _body.Clear();
            _body.AddText(L.Text("Try another search in this section."));
            return;
        }

        InspectionContent.Write(_body, entry.Topic);

        if (entry.Table is not null)
        {
            var hasTiers = entry.Table.Tiers.Count > 0;
            _tier.Visible = hasTiers;
            _tier.TooltipText = L.Text("Select an affix tier to view its value ranges.");
            _search.Size = new(hasTiers ? 430 : 604, 44);
            _tier.Clear();

            for (var index = 0; index < entry.Table.Tiers.Count; index++)
            {
                _tier.AddItem(L.Format($"Tier T{7 - index}"));
            }

            var table = entry.Table;

            if (hasTiers)
            {
                _tierIndex = Math.Clamp(_tierIndex, 0, table.Tiers.Count - 1);
                _tier.Select(_tierIndex);
                table = table.Tiers[_tierIndex];
            }

            var top = wide ? 204 : 468;
            _table.Position = new(left, top);
            _table.Display(table, wide ? _search.Text : "", _filter, new(width, 822 - top));
        }

        foreach (var button in _list.GetChildren().OfType<Button>())
        {
            button.AddThemeColorOverride("font_color", button.GetMeta("reference_id").AsString() == entry.Id ? HonorTheme.Brass : HonorTheme.Parchment);
        }
    }

    private void FollowLink(Variant metadata)
    {
        if (_selected is not null && int.TryParse(metadata.AsString(), NumberStyles.None, CultureInfo.InvariantCulture, out var index)
            && index >= 0 && index < _selected.Topic.Links.Count)
        {
            InspectionRequested?.Invoke(_selected.Topic.Links[index].Open());
        }
    }

    private static string SectionFor(ReferenceEntry entry) => entry.Id.StartsWith("loot.monster.", StringComparison.Ordinal) ? "monsters"
        : entry.Id == "loot.affixes" ? "affixes"
        : entry.Id == "loot.bases" || entry.Id.StartsWith("item.", StringComparison.Ordinal) ? "items"
        : entry.Id is "session.rolls" or "session.combat" ? "journal" : "rules";

    private static string FilterName(string key) => L.Text(key switch
    {
        "weapon" => "Main hand",
        "body" => "Body armor",
        "helmet" => "Helmet",
        "boots" => "Boots",
        "offhand" => "Off hand",
        "amulet" => "Amulet",
        "ring" => "Ring",
        "gold" => "Gold",
        _ => key
    });
}
