namespace RollForHonor.Domain.Combat.Effects.Models;

/// <summary>Configures healing from actual opponent HP lost after damage and target health adjustments.</summary>
public sealed record LifestealDefinition : AttackBehaviorDefinition
{
    /// <summary>Gets the healing fraction from zero through one; healing rounds upward and final health is clamped.</summary>
    public decimal Fraction { get; }

    /// <summary>Creates a non-stacking attack behavior without applying a persistent status.</summary>
    /// <param name="fraction">The fraction of actual HP loss, excluding overkill.</param>
    /// <exception cref="ArgumentOutOfRangeException">The fraction is outside zero through one.</exception>
    public LifestealDefinition(decimal fraction)
    {
        if (fraction is < 0 or > 1) throw new ArgumentOutOfRangeException(nameof(fraction));
        Fraction = fraction;
    }
}
