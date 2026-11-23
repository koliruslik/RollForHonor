using System.Collections.Generic;
using System.Linq;
using Godot;

namespace RollForHonor.Godot.UI.Common;

/// <summary>Owns transient explanation branches and independently pinned, movable cards.</summary>
public partial class InspectionStack : Control
{
    /// <summary>Gets the number of open cards, including independently pinned explanations.</summary>
    public int Count => _entries.Count;

    private readonly List<Branch> _entries = new();
    private Vector2 _origin;
    private int _pinSequence;

    /// <inheritdoc />
    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Ignore;
        Hide();
    }

    /// <inheritdoc />
    public override void _Process(double delta)
    {
        Size = GetParent<Control>().Size;
        var automatic = _entries
            .Where(entry => !entry.Pane.HasCustomPosition)
            .ToArray();

        if (automatic.Length == 0)
        {
            return;
        }

        var columns = Mathf.Min(automatic.Length, Mathf.Max(1, Mathf.FloorToInt((Size.X - 20) / 372)));
        var width = columns * 372 - 12;
        var tallest = automatic.Max(entry => entry.Pane.Size.Y);
        var origin = _origin.Clamp(new Vector2(16, 16),
            new Vector2(Mathf.Max(16, Size.X - width - 16), Mathf.Max(16, Size.Y - tallest - 16)));

        for (var index = 0; index < automatic.Length; index++)
        {
            var pane = automatic[index].Pane;
            var point = origin + new Vector2(index % columns * 372, index / columns * 32);
            var limit = (Size - pane.Size - new Vector2(16, 16)).Max(new Vector2(16, 16));
            pane.Position = point.Clamp(new Vector2(16, 16), limit);
        }
    }

    /// <summary>Opens a new transient explanation while retaining pinned cards.</summary>
    /// <param name="topic">The captured explanation.</param>
    /// <param name="origin">The preferred position in the design surface.</param>
    public void Open(InspectionTopic topic, Vector2 origin)
    {
        Clear();
        _origin = origin;
        Size = GetParent<Control>().Size;
        Append(topic, null);
        Show();
    }

    /// <summary>Closes the most recently opened unpinned card, preserving independent pins.</summary>
    /// <returns>True if a transient explanation closed; false if only pins or no cards remain.</returns>
    public bool CloseLast()
    {
        var entry = _entries.LastOrDefault(candidate => !candidate.Pane.IsPinned);

        if (entry is null)
        {
            return false;
        }

        CloseBranch(entry);

        return true;
    }

    /// <summary>Closes source-owned cards while retaining independently pinned explanations.</summary>
    public void Clear() => Remove(_entries
        .Where(entry => !entry.Pane.IsPinned)
        .ToArray());

    private void Append(InspectionTopic topic, Branch parent)
    {
        if (parent is not null)
        {
            if (parent.Pane.IsPinned)
            {
                Clear();
                var right = parent.Pane.Position.X + 372;
                var x = right + 360 <= Size.X - 16 ? right : parent.Pane.Position.X - 372;
                _origin = new Vector2(x, parent.Pane.Position.Y);
            }
            else
            {
                Remove(_entries
                    .Where(entry => IsDescendant(entry, parent) && !entry.Pane.IsPinned)
                    .ToArray());
            }
        }

        var depth = parent is null ? 0 : parent.Depth + 1;
        var pane = new InspectionPane { Name = $"Level{depth}" };
        AddChild(pane, true);
        var branch = new Branch(pane, parent, depth);
        _entries.Add(branch);
        pane.TopicRequested += next => Append(next, branch);
        pane.Closed += () => CloseBranch(branch);
        pane.PinnedChanged += () => pane.Name = $"Retained{++_pinSequence}";
        pane.ShowTopic(topic);
        Show();
    }

    private static bool IsDescendant(Branch candidate, Branch ancestor)
    {
        for (var parent = candidate.Parent; parent is not null; parent = parent.Parent)
        {
            if (parent == ancestor)
            {
                return true;
            }
        }

        return false;
    }

    private void CloseBranch(Branch branch) => Remove(_entries
        .Where(entry => entry == branch || IsDescendant(entry, branch) && !entry.Pane.IsPinned)
        .ToArray());

    private void Remove(IReadOnlyList<Branch> entries)
    {
        foreach (var entry in entries)
        {
            _entries.Remove(entry);
            RemoveChild(entry.Pane);
            entry.Pane.QueueFree();
        }

        foreach (var entry in _entries)
        {
            if (entry.Parent is not null && !_entries.Contains(entry.Parent))
            {
                entry.Parent = null;
            }
        }

        Visible = _entries.Count > 0;
    }

    private sealed class Branch(InspectionPane pane, Branch parent, int depth)
    {
        internal InspectionPane Pane { get; } = pane;
        internal Branch Parent { get; set; } = parent;
        internal int Depth { get; } = depth;
    }
}
