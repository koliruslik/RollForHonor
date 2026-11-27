namespace RollForHonor.Domain.Combat.Effects.Models;

/// <summary>Identifies production on-hit periodic statuses.</summary>
public enum StatusEffectKind
{
    /// <summary>Independent pure-damage stacks.</summary>
    Poison,

    /// <summary>A single fire effect whose remaining duration can refresh.</summary>
    Ignite,

    /// <summary>A single low-damage wound with movement slowing.</summary>
    Bleed
}

/// <summary>Shares stable status identities between runtime rules and presentation.</summary>
public static class StatusEffectIds
{
    /// <summary>Gets the production poison definition identity.</summary>
    public static EffectDefinitionId Poison { get; } = new(Guid.Parse("d0500000-0000-0000-0000-000000000003"));

    /// <summary>Gets the production ignite definition identity, separate from the laboratory fixture.</summary>
    public static EffectDefinitionId Ignite { get; } = new(Guid.Parse("d0500000-0000-0000-0000-000000000004"));

    /// <summary>Gets the production bleed definition identity, separate from the laboratory fixture.</summary>
    public static EffectDefinitionId Bleed { get; } = new(Guid.Parse("d0500000-0000-0000-0000-000000000005"));
}
