using Godot;
using RollForHonor.Domain.Combat.Damage.Models;
using RollForHonor.Domain.Stats;

namespace RollForHonor.Godot.Gameplay.Stats;

/// <summary>Provides an editable modifier definition without moving calculation rules out of Domain.</summary>
[GlobalClass]
public partial class StatModifierDefinition : Resource
{
    /// <summary>Gets or sets the affected attribute category.</summary>
    [Export]
    public StatType Type { get; set; }

    /// <summary>Gets or sets whether outgoing damage is qualified by DamageKind; resistance is always qualified.</summary>
    [Export]
    public bool UseDamageKind { get; set; }

    /// <summary>Gets or sets the damage category used for qualified modifiers.</summary>
    [Export]
    public DamageType DamageKind { get; set; }

    /// <summary>Gets or sets how the modifier stacks.</summary>
    [Export]
    public StatOperation Operation { get; set; }

    /// <summary>Gets or sets an absolute amount or fractional Increased/More value, where 0.2 means 20%.</summary>
    [Export]
    public float Amount { get; set; }

    /// <summary>Gets or sets the stable content origin identifier.</summary>
    [Export]
    public string Source { get; set; } = "definition";

    /// <summary>Copies editor content into a validated immutable Domain modifier.</summary>
    /// <returns>The calculation input, independent of later Resource edits.</returns>
    public StatModifier CreateModifier() => new(
        new StatKey(Type, UseDamageKind || Type == StatType.Resistance ? DamageKind : null),
        Operation, (decimal)Amount, Source);
}
