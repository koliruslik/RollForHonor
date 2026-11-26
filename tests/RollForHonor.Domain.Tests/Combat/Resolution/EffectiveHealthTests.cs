using RollForHonor.Domain.Combat.Damage.Models;
using RollForHonor.Domain.Combat.Damage.Services;
using RollForHonor.Domain.Combat.State.Models;

namespace RollForHonor.Domain.Tests.Combat.Resolution;

/// <summary>Verifies displayed EHP against agreed exact controls and the real quality/defense rules.</summary>
public sealed class EffectiveHealthTests
{
    /// <summary>Reference armor/evasion sets reproduce the approved normalized EHP values.</summary>
    [Theory]
    [InlineData(0, 0, 100)]
    [InlineData(130, 0, 142.857143)]
    [InlineData(0, 100, 135.714286)]
    [InlineData(45, 45, 130.136986)]
    public void AgreedReferenceSetsMatch(int armor, int evasion, decimal expected)
    {
        var target = new CombatantSnapshot(new(Guid.NewGuid()), 100, 100, armor, evasion, [], []);
        var actual = new EffectiveHealthCalculator().Calculate(target);
        Assert.InRange(actual!.Value, expected - 0.00001m, expected + 0.00001m);
    }

    /// <summary>Armor depends on incoming magnitude and cannot be summarized by a context-free reduction.</summary>
    [Theory]
    [InlineData(5, 166.666667)]
    [InlineData(10, 142.857143)]
    [InlineData(40, 108.108108)]
    public void HitMagnitudeChangesArmorValue(int magnitude, decimal expected)
    {
        var target = new CombatantSnapshot(new(Guid.NewGuid()), 1, 100, 130, 0, [], []);
        Assert.InRange(new EffectiveHealthCalculator().Calculate(target, hitDamage: magnitude)!.Value,
            expected - 0.00001m, expected + 0.00001m);
        Assert.Equal(1, target.Health);
    }

    /// <summary>Typed immunity, weakness and the physical minimum follow the current defense pipeline.</summary>
    [Fact]
    public void ResistancesAndImmunityUseCurrentRules()
    {
        var id = new CombatantId(Guid.NewGuid());
        var calculator = new EffectiveHealthCalculator();
        var resistant = new CombatantSnapshot(id, 100, 100, 0, 0, [new(DamageType.Fire, 0.5m)], []);
        Assert.Equal(200m, calculator.Calculate(resistant, DamageType.Fire));
        var immune = new CombatantSnapshot(id, 100, 100, 0, 0, [new(DamageType.Fire, 1)], []);
        Assert.Null(calculator.Calculate(immune, DamageType.Fire));
        var physicalFloor = new CombatantSnapshot(id, 100, 100, 0, 0, [new(DamageType.Piercing, 1)], []);
        Assert.Equal(1000m, calculator.Calculate(physicalFloor));
        var weak = new CombatantSnapshot(id, 100, 100, 0, 0, [new(DamageType.Fire, -0.5m)], []);
        Assert.InRange(calculator.Calculate(weak, DamageType.Fire)!.Value, 66.66666m, 66.66667m);
    }

    /// <summary>Fractional evasion survives the d20 comparison; invalid reference damage is rejected.</summary>
    [Fact]
    public void PreservesFractionalEvasionAndRejectsInvalidContext()
    {
        var calculator = new EffectiveHealthCalculator();
        var id = new CombatantId(Guid.NewGuid());
        var rounded = new CombatantSnapshot(id, 100, 100, 0, 40, [], []);
        var fractional = new CombatantSnapshot(id, 100, 100, 0, 40, [], [], evasionRating: 40.5m);
        Assert.True(calculator.Calculate(fractional) > calculator.Calculate(rounded));
        Assert.Throws<ArgumentOutOfRangeException>(() => calculator.Calculate(rounded, hitDamage: 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => calculator.Calculate(rounded, (DamageType)999));
    }

    /// <summary>Mixed damage resolves each typed component, preserving immunity and per-component rounding.</summary>
    [Fact]
    public void MixedHitsUseActualComponentsRatherThanAveragingSingleTypeEhp()
    {
        var calculator = new EffectiveHealthCalculator();
        var target = new CombatantSnapshot(new(Guid.NewGuid()), 100, 100, 0, 0,
            [new(DamageType.Fire, 1)], []);
        var mixed = new DamagePacket([new(DamageType.Piercing, 5), new(DamageType.Fire, 5)]);
        Assert.Equal(200m, calculator.Calculate(target, mixed));
        Assert.Equal(100m, calculator.Calculate(target, new DamagePacket([new(DamageType.Cold, 5), new(DamageType.Lightning, 5)])));
        Assert.Throws<ArgumentOutOfRangeException>(() => calculator.Calculate(target, new DamagePacket([])));
    }


    /// <summary>Rounding is applied to each mixed component before defenses and quality averaging.</summary>
    [Fact]
    public void MixedHitKeepsQualityRoundingPerComponent()
    {
        var calculator = new EffectiveHealthCalculator();
        var target = new CombatantSnapshot(new(Guid.NewGuid()), 100, 100, 0, 100, [], []);
        var mixed = new DamagePacket([new(DamageType.Fire, 5), new(DamageType.Cold, 5)]);
        Assert.InRange(calculator.Calculate(target, mixed)!.Value, 126.66666m, 126.66667m);
        Assert.NotEqual(calculator.Calculate(target, DamageType.Fire), calculator.Calculate(target, mixed));
    }

}
