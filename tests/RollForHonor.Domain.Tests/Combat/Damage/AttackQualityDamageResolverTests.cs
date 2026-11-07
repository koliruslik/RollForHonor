using RollForHonor.Domain.Combat.Attacks.Models;
using RollForHonor.Domain.Combat.Damage.Configuration;
using RollForHonor.Domain.Combat.Damage.Models;
using RollForHonor.Domain.Combat.Damage.Services;

namespace RollForHonor.Domain.Tests.Combat.Damage;

public sealed class AttackQualityDamageResolverTests
{
    private static AttackQualityDamageResolver Resolver() => new(new DamageMultipliers(0, 0.5m, 1, 2));

    [Fact]
    public void Constructor_NullMultipliersThrow() =>
        Assert.Throws<ArgumentNullException>(() => new AttackQualityDamageResolver(null!));

    [Theory]
    [InlineData(AttackOutcome.Evaded, 0)]
    [InlineData(AttackOutcome.GlancingHit, 2)]
    [InlineData(AttackOutcome.Hit, 4)]
    [InlineData(AttackOutcome.CriticalHit, 8)]
    public void Resolve_SelectsConfiguredMultiplier_AndPreservesInputHistory(AttackOutcome outcome, int expected)
    {
        var input = Damage(new DamageAmount(DamageType.Bludgeoning, 4));

        var result = Resolver().Resolve(input, outcome);

        Assert.Equal(expected, Assert.Single(result.Damage.Components).Amount);
        Assert.Equal(DamageType.Bludgeoning, result.Damage.Components[0].Type);
        Assert.Same(input.Breakdown.Steps[0], result.Breakdown.Steps[0]);
        var quality = result.Breakdown.Steps[1];
        Assert.Equal(DamageStepType.AttackQuality, quality.Type);
        Assert.Same(input.Damage, quality.Input);
        Assert.Same(result.Damage, quality.Output);
        Assert.Equal(4, input.Sum());
        Assert.Single(input.Breakdown.Steps);
    }

    [Fact]
    public void Resolve_FractionalMultiplierRoundsEachComponentUpward()
    {
        var input = Damage(new DamageAmount(DamageType.Piercing, 3), new DamageAmount(DamageType.Cold, 1));

        var result = Resolver().Resolve(input, AttackOutcome.GlancingHit);

        Assert.Equal(new[] { new DamageAmount(DamageType.Piercing, 2), new DamageAmount(DamageType.Cold, 1) },
            result.Damage.Components.ToArray());
    }

    [Fact]
    public void Resolve_EmptyDamageRemainsEmpty()
    {
        var result = Resolver().Resolve(Damage(), AttackOutcome.CriticalHit);

        Assert.Empty(result.Damage.Components);
        Assert.Equal(2, result.Breakdown.Steps.Count);
    }

    [Fact]
    public void Resolve_ScalingOverflowThrows() =>
        Assert.Throws<OverflowException>(() => Resolver().Resolve(
            Damage(new DamageAmount(DamageType.Fire, int.MaxValue)), AttackOutcome.CriticalHit));

    [Fact]
    public void Resolve_UnknownOutcomeThrows() =>
        Assert.Throws<InvalidOperationException>(() => Resolver().Resolve(Damage(), (AttackOutcome)999));

    [Fact]
    public void Resolve_NullDamageThrows() =>
        Assert.Throws<ArgumentNullException>(() => Resolver().Resolve(null!, AttackOutcome.Hit));

    private static DamageCalculationResult Damage(params DamageAmount[] amounts)
    {
        var packet = new DamagePacket(amounts);
        return new DamageCalculationResult(new DamageBreakdown([
            new DamageStep(DamageStepType.WeaponRoll, new DamagePacket([]), packet)
        ]), packet);
    }
}
