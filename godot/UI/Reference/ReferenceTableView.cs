using System;
using System.Linq;
using Godot;
using RollForHonor.Godot.UI.Common;
using RollForHonor.Godot.UI.Localization;

namespace RollForHonor.Godot.UI.Reference;

/// <summary>Renders colored catalog rows with fixed headings, scrolling and native inspection actions.</summary>
public partial class ReferenceTableView : Control
{
    /// <summary>Raised when a row's base properties should open as a normal inspection card.</summary>
    public event Action<InspectionTopic> InspectionRequested;

    /// <summary>Gets the number of rows remaining after the current search and filter.</summary>
    public int VisibleRowCount { get; private set; }

    /// <summary>Rebuilds the visible table from copied content without generating loot.</summary>
    /// <param name="table">The captured columns and rows.</param>
    /// <param name="query">Text to search within rows.</param>
    /// <param name="filter">An exact filter identity, empty for all rows.</param>
    /// <param name="size">The available design-space rectangle size.</param>
    public void Display(ReferenceTable table, string query, string filter, Vector2 size)
    {
        foreach (var child in GetChildren())
        {
            RemoveChild(child);
            child.QueueFree();
        }

        Size = size;
        var scale = (size.X - 24 - (table.Columns.Count - 1) * 2) / table.Columns.Sum(column => column.Width);
        var headings = new HBoxContainer { Name = "Headings", Size = new Vector2(size.X - 20, 42) };
        headings.AddThemeConstantOverride("separation", 2);
        AddChild(headings);

        foreach (var column in table.Columns)
        {
            headings.AddChild(column.Details is null
                ? Cell(column.Title, column.Width * scale, HonorTheme.Brass, true)
                : LinkedCell(column.Title, column.Width * scale, HonorTheme.Brass, column.Details, true));
        }

        var scroll = UiLayout.Place(this, new ScrollContainer
        {
            Name = "RowsScroll",
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
            MouseForcePassScrollEvents = false
        }, new Rect2(0, 44, size.X, size.Y - 44));
        var rows = new VBoxContainer { Name = "Rows", SizeFlagsHorizontal = SizeFlags.ExpandFill };
        rows.AddThemeConstantOverride("separation", 3);
        scroll.AddChild(rows);
        var visible = table.Rows
            .Where(row => row.Matches(query, filter))
            .ToArray();
        VisibleRowCount = visible.Length;

        foreach (var row in visible)
        {
            var panel = new PanelContainer { CustomMinimumSize = new Vector2(0, 52), SizeFlagsHorizontal = SizeFlags.ExpandFill };
            panel.SetMeta("reference_row_id", row.Id);
            var style = HonorTheme.Panel(new Color(row.Color, 0.045f), new Color(row.Color, 0.45f), 2);
            style.BorderWidthLeft = 3;
            panel.AddThemeStyleboxOverride("panel", style);
            var cells = new HBoxContainer();
            cells.AddThemeConstantOverride("separation", 2);
            panel.AddChild(cells);
            rows.AddChild(panel);

            for (var index = 0; index < table.Columns.Count; index++)
            {
                var width = table.Columns[index].Width * scale;

                var topic = row.CellDetails?[index] ?? (index == 0 ? row.Details : null);
                var color = index == 0 || row.Color == HonorTheme.Muted ? row.Color : HonorTheme.Parchment;
                cells.AddChild(topic is null ? Cell(row.Cells[index], width, color)
                    : LinkedCell(row.Cells[index], width, color, topic));
            }
        }

        if (visible.Length == 0)
        {
            rows.AddChild(UiLayout.Text(L.Text("No matching rows. Clear the search or choose another filter."), 18, HonorTheme.Muted));
        }
    }

    private Button LinkedCell(string text, float width, Color color, InspectionTopic topic, bool heading = false)
    {
        var button = new Button
        {
            Text = L.Refresh(text),
            CustomMinimumSize = new Vector2(width, heading ? 38 : 46),
            SizeFlagsHorizontal = SizeFlags.ShrinkBegin,
            Alignment = HorizontalAlignment.Left,
            ClipText = false,
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            FocusMode = FocusModeEnum.All,
            TooltipText = topic.Description + L.Text("\nClick for details and related explanations."),
            MouseDefaultCursorShape = CursorShape.PointingHand
        };
        button.AddThemeFontSizeOverride("font_size", heading ? 15 : 16);
        button.AddThemeColorOverride("font_color", color);
        button.AddThemeColorOverride("font_hover_color", color.Lightened(0.15f));
        button.AddThemeColorOverride("font_pressed_color", color);

        foreach (var state in new[] { "normal", "hover", "pressed", "focus" })
        {
            button.AddThemeStyleboxOverride(state, new StyleBoxEmpty());
        }

        button.Pressed += () => InspectionRequested?.Invoke(topic);
        return button;
    }

    private static Label Cell(string text, float width, Color color, bool heading = false)
    {
        var label = UiLayout.Text(L.Refresh(text), heading ? 15 : 16, color);
        label.CustomMinimumSize = new Vector2(width, heading ? 38 : 46);
        label.SizeFlagsHorizontal = SizeFlags.ShrinkBegin;
        label.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        label.VerticalAlignment = VerticalAlignment.Center;
        label.TooltipText = L.Refresh(text);

        return label;
    }
}
