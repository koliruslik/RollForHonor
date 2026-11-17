#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using RollForHonor.Domain.Combat.Attacks.Models;
using RollForHonor.Domain.Combat.Damage.Models;
using RollForHonor.Domain.Combat.State.Models;

namespace RollForHonor.Godot.CombatDemo;

/// <summary>Edits explicit test-scene state and the next attack's payload without implementing combat rules.</summary>
public partial class DemoStatsPanel : VBoxContainer
{
    private readonly Dictionary<string, SpinBox> _numbers = [];
    private OptionButton _damageType = null!;
    private CheckBox _burning = null!;
    private CheckBox _bleeding = null!;

    /// <summary>Raised when the user explicitly applies all draft state values, including current HP.</summary>
    public event Action? ApplyRequested;

    /// <summary>Builds the form from initial state and explicit fixture weapon values.</summary>
    /// <param name="title">The participant display name.</param>
    /// <param name="initial">Initial immutable combat values.</param>
    /// <param name="modifier">Initial flat attack modifier.</param>
    /// <param name="flatDamage">Initial flat weapon damage.</param>
    public void Configure(string title, CombatantSnapshot initial, int modifier, int flatDamage)
    {
        AddChild(new Label { Text = title, Name = "Title" });
        AddChild(new Label { Text = "Draft stats | Apply includes current HP", AutowrapMode = TextServer.AutowrapMode.WordSmart });
        AddNumber("Health", "Current HP", 0, 100000, initial.Health);
        AddNumber("MaxHealth", "Maximum HP", 1, 100000, initial.MaxHealth);
        AddNumber("Armor", "Armor", -10000, 100000, initial.Armor);
        AddNumber("Evasion", "Evasion", -10000, 10000, initial.Evasion);
        AddNumber("Modifier", "Attack modifier", -1000, 1000, modifier);
        AddNumber("DiceCount", "Damage dice count", 1, 20, 1);
        AddNumber("DiceSides", "Damage die sides", 1, 1000, 6);
        AddNumber("FlatDamage", "Flat damage", -10000, 10000, flatDamage);
        _damageType = new OptionButton { Name = "DamageType" };
        foreach (var type in Enum.GetValues<DamageType>()) _damageType.AddItem(type.ToString(), (int)type);
        AddChild(_damageType);
        AddChild(new Label { Text = "Resistance % | negative = weakness" });
        foreach (var type in Enum.GetValues<DamageType>())
            AddNumber(type.ToString(), type.ToString(), -1000, 100, (double)(initial.Resistances.SingleOrDefault(value => value.Type == type)?.Value ?? 0) * 100);
        _burning = new CheckBox { Name = "Burning", Text = "Burning on contact (apply only)" };
        _bleeding = new CheckBox { Name = "Bleeding", Text = "Bleeding on contact (apply only)" };
        AddChild(_burning);
        AddChild(_bleeding);
        AddChild(new CheckBox { Text = "Vampirism - next effect branch", Disabled = true,
            TooltipText = "Requires production effect behavior and a selected healing rule." });
        AddChild(new Label { Text = "Test statuses: 1d4 Fire / Slashing.\nNo automatic ticks yet. Contact includes evasion.",
            AutowrapMode = TextServer.AutowrapMode.WordSmart });
        var apply = new Button { Text = "Apply draft stats (including HP)", Name = "Apply" };
        apply.Pressed += () => ApplyRequested?.Invoke();
        AddChild(apply);
    }

    /// <summary>Builds a validated state edit while retaining currently active status instances.</summary>
    /// <param name="current">The participant's live state, supplying identity and effects.</param>
    /// <returns>A complete replacement view; invalid HP bounds are rejected by the Domain model.</returns>
    public CombatantSnapshot BuildState(CombatantSnapshot current) => new(current.Id, Value("Health"), Value("MaxHealth"),
        Value("Armor"), Value("Evasion"), Enum.GetValues<DamageType>()
            .Select(type => new ResistanceValue(type, (decimal)_numbers[type.ToString()].Value / 100)).ToArray(), current.Effects);

    /// <summary>Builds the next attack's immutable fixture payload from the current weapon/effect selections.</summary>
    /// <returns>A d20 attack with one configured typed damage component and selected existing statuses.</returns>
    public AttackPayload BuildAttack() => new(new(1, 20), Value("Modifier"), NaturalOutcomeShiftRules.Standard,
        [new(new(Value("DiceCount"), Value("DiceSides")), Value("FlatDamage"), (DamageType)_damageType.GetSelectedId())],
        DemoEffectCatalog.Select(_burning.ButtonPressed, _bleeding.ButtonPressed), AttackTags.Projectile);

    private int Value(string key) => checked((int)_numbers[key].Value);
    private void AddNumber(string key, string label, double minimum, double maximum, double value)
    {
        var row = new HBoxContainer { Name = key + "Row" };
        row.AddChild(new Label { Text = label, SizeFlagsHorizontal = SizeFlags.ExpandFill });
        var number = new SpinBox { Name = key, MinValue = minimum, MaxValue = maximum, Step = 1, Value = value,
            CustomMinimumSize = new(115, 0) };
        _numbers.Add(key, number);
        row.AddChild(number);
        AddChild(row);
    }
}
