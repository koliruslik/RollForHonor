using System.Collections.Generic;
using System.Linq;
using Godot;
using RollForHonor.Domain.Combat.Damage.Models;
using RollForHonor.Domain.Stats;

namespace RollForHonor.Godot.Gameplay.Stats;

/// <summary>Stores editable base attributes and modifier content shared by player and enemy definitions.</summary>
[GlobalClass]
public partial class ActorStatDefinition : Resource
{
    /// <summary>Gets or sets allocated Strength points, without a derived combat formula.</summary>
    [Export]
    public int Strength { get; set; }

    /// <summary>Gets or sets allocated Dexterity points, without a derived combat formula.</summary>
    [Export]
    public int Dexterity { get; set; }

    /// <summary>Gets or sets allocated Constitution points, without a derived combat formula.</summary>
    [Export]
    public int Constitution { get; set; }

    /// <summary>Gets or sets allocated Intelligence points, without a derived combat formula.</summary>
    [Export]
    public int Intelligence { get; set; }

    /// <summary>Gets or sets allocated Luck points; no loot or dice bonus is inferred.</summary>
    [Export]
    public int Luck { get; set; }

    /// <summary>Gets or sets base maximum HP; current HP belongs to runtime state.</summary>
    [Export]
    public int MaxHealth { get; set; } = 1;

    /// <summary>Gets or sets base maximum mana; current mana belongs to runtime state.</summary>
    [Export]
    public int MaxMana { get; set; }

    /// <summary>Gets or sets base armor for the existing defense formula.</summary>
    [Export]
    public int Armor { get; set; }

    /// <summary>Gets or sets base d20 evasion.</summary>
    [Export]
    public int Evasion { get; set; }

    /// <summary>Gets or sets base movement speed in world units per second.</summary>
    [Export]
    public float MovementSpeed { get; set; }

    /// <summary>Gets or sets the source bonus added to attack-specific d20 modifiers.</summary>
    [Export]
    public int AttackModifier { get; set; }

    /// <summary>Gets or sets reserved fractional critical chance; current d20 rules do not consume it.</summary>
    [Export]
    public float CriticalChance { get; set; }

    /// <summary>Gets or sets the reserved independent critical multiplier.</summary>
    [Export]
    public float CriticalMultiplier { get; set; } = 1;

    /// <summary>Gets or sets base fractional resistance by damage type, where 0.25 means 25%.</summary>
    [Export]
    public global::Godot.Collections.Dictionary<DamageType, float> Resistances { get; set; } = new();

    /// <summary>Gets or sets content modifiers copied when a runtime profile is created.</summary>
    [Export]
    public global::Godot.Collections.Array<StatModifierDefinition> Modifiers { get; set; } = [];

    /// <summary>Copies resource content into immutable base values and modifiers.</summary>
    /// <returns>The shared player/enemy stat-calculation input.</returns>
    public StatProfile CreateProfile()
    {
        var values = new List<KeyValuePair<StatKey, decimal>>
        {
            new(new(StatType.Strength), Strength),
            new(new(StatType.Dexterity), Dexterity),
            new(new(StatType.Constitution), Constitution),
            new(new(StatType.Intelligence), Intelligence),
            new(new(StatType.Luck), Luck),
            new(new(StatType.MaxHealth), MaxHealth),
            new(new(StatType.MaxMana), MaxMana),
            new(new(StatType.Armor), Armor),
            new(new(StatType.Evasion), Evasion),
            new(new(StatType.MovementSpeed), (decimal)MovementSpeed),
            new(new(StatType.AttackModifier), AttackModifier),
            new(new(StatType.CriticalChance), (decimal)CriticalChance),
            new(new(StatType.CriticalMultiplier), (decimal)CriticalMultiplier)
        };
        values.AddRange(Resistances.Select(pair =>
            new KeyValuePair<StatKey, decimal>(new(StatType.Resistance, pair.Key), (decimal)pair.Value)));

        return new StatProfile(new StatBlock(values), Modifiers.Select(modifier => modifier.CreateModifier()));
    }
}
