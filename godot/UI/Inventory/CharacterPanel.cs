using System;
using System.Collections.Generic;
using System.Globalization;
using Godot;
using RollForHonor.Domain.Combat.Damage.Models;
using RollForHonor.Domain.Combat.State.Models;
using RollForHonor.Domain.Stats;
using RollForHonor.Godot.UI.Common;
using RollForHonor.Godot.Gameplay.Input;
using RollForHonor.Godot.UI.Localization;

namespace RollForHonor.Godot.UI.Inventory;

/// <summary>Displays current runtime resources and resolved combat stats in the wireframe's left-hand scrollable panel.</summary>
public partial class CharacterPanel : Control
{
    /// <summary>Raised when the player closes the character panel.</summary>
    public event Action Closed;

    /// <summary>Raised when an underlined combat stat opens its explanation.</summary>
    public event Action<InspectionTopic> InspectionRequested;

    private readonly Dictionary<StatKey, Label> _values = new();
    private Label _health;
    private Label _mana;
    private Label _hints;

    /// <inheritdoc />
    public override void _Ready()
    {
        var panel = UiLayout.Panel(this, "Character", new Rect2(0, 0, 360, 1080));
        UiLayout.Place(panel, UiLayout.Text(L.Text("Character"), 24, heading: true), new Rect2(20, 16, 260, 48));
        UiLayout.Place(panel, UiLayout.CloseButton(() => Closed?.Invoke()), new Rect2(300, 20, 40, 40));
        UiLayout.Divider(panel, new Rect2(20, 80, 320, 2));
        var scroll = UiLayout.Place(panel, new ScrollContainer { HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled }, new Rect2(20, 96, 320, 900));
        var rows = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        rows.AddThemeConstantOverride("separation", 6);
        var inset = new MarginContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        inset.AddThemeConstantOverride("margin_right", 16);
        scroll.AddChild(inset);
        inset.AddChild(rows);
        BuildResources(rows);
        BuildCombat(rows);
        BuildResistances(rows);
        _hints = UiLayout.Place(panel, UiLayout.Text("", 12, new Color(HonorTheme.Muted, 0.55f)), new Rect2(20, 1016, 320, 48));
        _hints.Name = "CharacterHints";
        Visible = false;
    }

    /// <summary>Updates displayed values from authoritative snapshots, without recomputing any gameplay formula.</summary>
    /// <param name="stats">Resolved stats read from the current session.</param>
    /// <param name="baseStats">Unmodified starting values used as the movement-speed percentage baseline.</param>
    /// <param name="actor">The actor's current HP and combat view.</param>
    /// <param name="mana">The current mana resource balance.</param>
    public void Refresh(StatBlock stats, StatBlock baseStats, CombatantSnapshot actor, decimal mana)
    {
        L.SetText(_hints, L.Format($"Scroll  Browse stats\n{GameInputBindings.Name("rfh_character")}  Close character"));

        _health.Text = $"{actor.Health} / {actor.MaxHealth}";
        _mana.Text = $"{mana:0} / {stats.Get(new StatKey(StatType.MaxMana)):0}";

        foreach (var pair in _values)
        {
            pair.Value.Text = Format(pair.Key, stats.Get(pair.Key), baseStats);
            pair.Value.GetParent<Control>().Visible = pair.Key.Type != StatType.AddedDamage || stats.Get(pair.Key) != 0;
        }
    }

    private void BuildResources(VBoxContainer rows)
    {
        Section(rows, L.Text("VITALITY"));
        _health = Row(rows, L.Text("Health"));
        _mana = Row(rows, L.Text("Mana"));
        AddStat(rows, L.Text("Movement speed"), new StatKey(StatType.MovementSpeed));
    }

    private void BuildCombat(VBoxContainer rows)
    {
        Section(rows, L.Text("OFFENSE"));
        AddStat(rows, L.Text("Attack roll bonus"), new StatKey(StatType.AttackModifier));

        foreach (var type in Enum.GetValues<DamageType>())
        {
            AddStat(rows, L.Format($"{type} damage"), new StatKey(StatType.DamageMultiplier, type));
            AddStat(rows, L.Format($"{type} flat damage"), new StatKey(StatType.AddedDamage, type));
        }

        Section(rows, L.Text("DEFENSE"));
        AddStat(rows, L.Text("Armor"), new StatKey(StatType.Armor));
        AddStat(rows, L.Text("Evasion"), new StatKey(StatType.Evasion));
    }

    private void BuildResistances(VBoxContainer rows)
    {
        Section(rows, L.Text("RESISTANCES"));

        foreach (var type in Enum.GetValues<DamageType>())
        {
            AddStat(rows, L.Text(type.ToString()), new StatKey(StatType.Resistance, type));
        }
    }

    private static string Format(StatKey key, decimal value, StatBlock baseStats) => key.Type switch
    {
        StatType.Resistance => (value * 100).ToString("0.#", CultureInfo.InvariantCulture) + "%",
        StatType.MovementSpeed => FormatMovementBonus(value, baseStats.Get(key)),
        StatType.DamageMultiplier => ((value - 1) * 100).ToString("+0.##;-0.##;0", CultureInfo.InvariantCulture) + "%",
        StatType.AddedDamage => value.ToString("+0.##;-0.##;0", CultureInfo.InvariantCulture),
        StatType.AttackModifier => value.ToString("+0;-0;0", CultureInfo.InvariantCulture),
        _ => value.ToString("0.##", CultureInfo.InvariantCulture)
    };

    private static string FormatMovementBonus(decimal speed, decimal baseSpeed)
    {
        if (baseSpeed <= 0)
        {
            return "—";
        }

        var bonus = (speed / baseSpeed - 1) * 100;

        return bonus.ToString("+0.##;-0.##;0", CultureInfo.InvariantCulture) + "%";
    }

    private static void Section(VBoxContainer rows, string title)
    {
        var label = UiLayout.Text(title, 15, HonorTheme.Brass);
        label.CustomMinimumSize = new Vector2(0, 32);
        rows.AddChild(label);
    }

    private Label Row(VBoxContainer rows, string name, StatKey? key = null)
    {
        var row = new HBoxContainer { CustomMinimumSize = new Vector2(0, 28) };
        var linked = key is { } stat && (stat.DamageKind is not null || stat.Type == StatType.Armor);
        var heading = linked
            ? new RichTextLabel { FitContent = true, ScrollActive = false, MetaUnderlined = true, SizeFlagsHorizontal = SizeFlags.ExpandFill, MouseFilter = MouseFilterEnum.Stop }
            : (Control)new Label { Text = name, SizeFlagsHorizontal = SizeFlags.ExpandFill, ClipText = true };
        row.AddChild(heading);
        if (heading is Label label)
        {
            L.SetText(label, name);
        }

        var value = UiLayout.Text("", 18, HonorTheme.Parchment);
        value.CustomMinimumSize = new Vector2(84, 0);
        value.HorizontalAlignment = HorizontalAlignment.Right;
        row.AddChild(value);
        heading.AddThemeFontSizeOverride(linked ? "normal_font_size" : "font_size", 18);

        if (heading is RichTextLabel link)
        {
            ConfigureStatLink(link, value, name, key.Value);
        }

        rows.AddChild(row);

        return value;
    }

    private void ConfigureStatLink(RichTextLabel link, Label value, string name, StatKey key)
    {
        var color = CombatGlossary.StatColor(key);
        InspectionText.Link(link, name, "inspect", color);
        L.OnChanged(link, () =>
        {
            link.Clear();
            InspectionText.Link(link, name, "inspect", color);
        });
        value.AddThemeColorOverride("font_color", color);
        link.MetaClicked += _ =>
        {
            var topic = CombatGlossary.Stat(key);
            InspectionRequested?.Invoke(new InspectionTopic(topic.Title, topic.Description, topic.Accent, topic.Links,
                [new InspectionFact(value.Text, color)]));
        };
    }

    private void AddStat(VBoxContainer rows, string name, StatKey key) => _values.Add(key, Row(rows, name, key));
}
