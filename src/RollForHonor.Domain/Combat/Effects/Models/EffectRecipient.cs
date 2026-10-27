namespace RollForHonor.Domain.Combat.Effects.Models;

/// <summary>Selects the recipient of a status carried by an attack.</summary>
public enum EffectRecipient
{
    /// <summary>The source of the action receives the status.</summary>
    Source,
    /// <summary>The target of the action receives the status.</summary>
    Target
}
