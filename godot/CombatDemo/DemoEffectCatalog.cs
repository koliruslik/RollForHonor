using System;
using System.Collections.Generic;
using System.Linq;
using RollForHonor.Domain.Combat.Damage.Models;
using RollForHonor.Domain.Combat.Effects.Models;

namespace RollForHonor.Godot.CombatDemo;

/// <summary>Names existing status definitions for the test scene; this is fixture content, not a Domain behavior registry.</summary>
public static class DemoEffectCatalog
{
    /// <summary>Gets the stable test burning definition; application is supported, scheduling is not configured.</summary>
    public static PeriodicDamageEffectDefinition Burning { get; } = new(
        new(Guid.Parse("d0500000-0000-0000-0000-000000000001")), [new(new(1, 4), 0, DamageType.Fire)]);
    /// <summary>Gets a test bleeding fixture using slashing damage, without defining a new production damage type.</summary>
    public static PeriodicDamageEffectDefinition Bleeding { get; } = new(
        new(Guid.Parse("d0500000-0000-0000-0000-000000000002")), [new(new(1, 4), 0, DamageType.Slashing)]);

    /// <summary>Returns selected immutable status definitions to attach to an attack payload.</summary>
    /// <param name="burning">Whether to include the burning fixture.</param>
    /// <param name="bleeding">Whether to include the bleeding fixture.</param>
    /// <returns>Definitions in stable burning/bleeding order.</returns>
    public static IReadOnlyList<CombatEffectDefinition> Select(bool burning, bool bleeding) =>
        new[] { (burning, Burning), (bleeding, Bleeding) }.Where(item => item.Item1)
            .Select(item => (CombatEffectDefinition)item.Item2).ToArray();

    /// <summary>Looks up a display name without making unregistered definitions look known.</summary>
    /// <param name="id">The definition identity recorded by combat.</param>
    /// <returns>The fixture name or the original identity.</returns>
    public static string Name(EffectDefinitionId id) => id == Burning.Id ? "Burning" : id == Bleeding.Id ? "Bleeding" : id.ToString();
}
