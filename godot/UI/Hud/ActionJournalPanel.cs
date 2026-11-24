using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using RollForHonor.Application.Combat.Models;
using RollForHonor.Domain.Combat.Resolution.Models;
using RollForHonor.Domain.Combat.Attacks.Models;
using RollForHonor.Domain.Combat.State.Models;
using RollForHonor.Godot.UI.Common;
using RollForHonor.Godot.UI.Localization;

namespace RollForHonor.Godot.UI.Hud;

/// <summary>Retains bounded action entries, including combat facts and immutable inspection links.</summary>
public partial class ActionJournalPanel : Control
{
    /// <summary>Raised when a journal entry opens a linked explanation.</summary>
    public event Action<InspectionTopic> InspectionRequested;

    /// <summary>Gets the latest actual d20 result before bonuses, or null before an attack roll.</summary>
    public int? LastRoll { get; private set; }

    private readonly Queue<Entry> _entries = new();
    private readonly Queue<InspectionTopic> _rolls = new();
    private readonly Dictionary<string, InspectionTopic> _links = new();
    private RichTextLabel _text;
    private Panel _panel;
    private bool _expanded;
    private Button _toggle;

    /// <inheritdoc />
    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Ignore;
        _panel = UiLayout.Panel(this, "Journal", new Rect2(0, 0, 400, 230));
        _panel.AddThemeStyleboxOverride("panel", HonorTheme.Panel(new Color(HonorTheme.Ink, 0.8f), new Color(HonorTheme.Border, 0.5f)));
        UiLayout.Place(_panel, UiLayout.Text(L.Text("ACTIONS"), 13, HonorTheme.Muted), new Rect2(16, 8, 200, 28));
        _toggle = UiLayout.Place(_panel, UiLayout.Button(L.Text("Details"), ToggleDetails), new Rect2(280, 8, 104, 40));
        _toggle.AddThemeFontSizeOverride("font_size", 13);
        _text = UiLayout.Place(_panel, new RichTextLabel { Name = "Entries", MetaUnderlined = true, MouseFilter = MouseFilterEnum.Stop }, new Rect2(16, 56, 368, 156));
        _text.AddThemeFontSizeOverride("normal_font_size", 14);
        _text.MetaClicked += metadata =>
        {
            if (_links.TryGetValue(metadata.AsString(), out var topic))
            {
                InspectionRequested?.Invoke(topic);
            }
        };
        Refresh();
        L.OnChanged(this, Refresh);
    }

    /// <summary>Retains only actually committed calculation facts.</summary>
    /// <param name="source">The source display name.</param>
    /// <param name="contact">The contact and publication result.</param>
    /// <param name="input">The views read before contact.</param>
    /// <param name="target">The target display name.</param>
    public void Record(string source, AttackContactResult contact, CombatStateRead input, string target)
    {
        if (contact.Impact is not { IsCommitted: true, Outcome: ResolvedCombat resolved })
        {
            return;
        }

        var result = resolved.Resolution.Result.Target;
        source = L.Text(source);
        target = L.Text(target);
        var before = result.DamageResolution?.BeforeDefenses.Sum() ?? 0;
        var after = result.DamageResolution?.AfterDefenses.Sum() ?? 0;
        var outcome = OutcomeName(result.Outcome);
        var dice = CombatInspectionTopics.Dice(source, target, resolved);
        var damage = CombatInspectionTopics.Damage(result);
        var topic = new InspectionTopic(L.Format($"{source} → {target}"), L.Format($"{outcome}\nDamage {before} → {after} (blocked {before - after})"), HonorTheme.Secondary,
            [new(L.Text("Dice and bonuses"), HonorTheme.Secondary, () => dice), new(L.Text("Damage breakdown"), HonorTheme.Secondary, () => damage)]);
        Enqueue(_entries, new Entry(source, target, outcome, before, after, topic, dice, damage, null), 12);

        if (result.AttackRoll is { } roll)
        {
            LastRoll = roll.Roll.Total;
            Enqueue(_rolls, dice, 8);
        }

        Refresh();
    }

    /// <summary>Records a completed noncombat action without changing the retained d20 result.</summary>
    /// <param name="message">A translated or retained formatted action description.</param>
    public void RecordAction(string message)
    {
        var topic = new InspectionTopic(L.Text("Action"), message, HonorTheme.Secondary);
        Enqueue(_entries, new Entry("", "", "", 0, 0, topic, null, null, message), 12);
        Refresh();
    }

    /// <summary>Captures the currently retained rolls for independent, pinnable inspection.</summary>
    /// <returns>A snapshot with links to each original roll and its bonuses.</returns>
    public InspectionTopic CreateDiceHistoryTopic() => new(L.Text("Recent rolls"),
        _rolls.Count == 0 ? L.Text("No rolls yet.") : L.Text("Select a recorded attack to inspect its dice and bonuses."), HonorTheme.Secondary,
        _rolls.Reverse()
            .Select(topic => new InspectionLink(topic.Title, HonorTheme.Secondary, () => topic)));

    /// <summary>Captures retained journal entries with the original damage stages and dice links.</summary>
    /// <returns>An independent history explanation; reading it does not repeat combat calculations.</returns>
    public InspectionTopic CreateActionHistoryTopic() => new(L.Text("Recent actions"),
        _entries.Count == 0 ? L.Text("No actions yet.") : L.Text("Select a recorded action to inspect its details."), HonorTheme.Secondary,
        _entries.Reverse()
            .Select(entry => new InspectionLink(entry.Action ?? entry.Topic.Title, HonorTheme.Secondary, () => entry.Topic)));

    private void Refresh()
    {
        _text.Clear();
        _links.Clear();

        if (_entries.Count == 0)
        {
            _text.AddText(L.Text("No actions yet."));
            return;
        }

        var visible = _entries
            .Reverse()
            .Take(_expanded ? 12 : 5);

        foreach (var entry in visible)
        {
            if (entry.Action is not null)
            {
                _text.AddText(L.Refresh(entry.Action) + "\n");
                continue;
            }

            _text.AddText(L.Format($"{entry.Source} → {entry.Target}: "));
            WriteLink(entry.Outcome, entry.Topic);
            _text.AddText(L.Text("\nDamage "));
            WriteLink($"{entry.Before} → {entry.After}", entry.Damage);
            _text.AddText(L.Format($"  (blocked {entry.Before - entry.After})"));

            if (_expanded)
            {
                _text.AddText("\n");
                WriteLink(L.Text("Dice and bonuses"), entry.Dice);
            }

            _text.AddText("\n");
        }
    }

    private void WriteLink(string text, InspectionTopic topic)
    {
        var id = _links.Count.ToString();
        _links[id] = topic;
        InspectionText.Link(_text, text, id, HonorTheme.Secondary);
    }

    private static string OutcomeName(AttackOutcome? outcome) => outcome switch
    {
        AttackOutcome.CriticalHit => L.Text("Critical hit"),
        AttackOutcome.GlancingHit => L.Text("Glancing hit"),
        AttackOutcome.Evaded => L.Text("Evaded"),
        AttackOutcome.Hit => L.Text("Hit"),
        _ => L.Text("Impact")
    };

    private static void Enqueue<T>(Queue<T> entries, T value, int limit)
    {
        entries.Enqueue(value);

        while (entries.Count > limit)
        {
            entries.Dequeue();
        }
    }

    private void ToggleDetails()
    {
        _expanded = !_expanded;
        Size = _panel.Size = new Vector2(400, _expanded ? 500 : 230);
        L.SetText(_toggle, _expanded ? L.Text("Compact") : L.Text("Details"));
        _text.Size = new Vector2(368, _expanded ? 426 : 156);
        Position = new Vector2(Position.X, _expanded ? 410 : 680);
        Refresh();
    }

    private sealed record Entry(string Source, string Target, string Outcome, int Before, int After,
        InspectionTopic Topic, InspectionTopic Dice, InspectionTopic Damage, string Action);
}
