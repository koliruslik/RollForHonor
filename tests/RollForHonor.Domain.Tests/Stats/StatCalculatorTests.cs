using RollForHonor.Domain.Combat.Damage.Models;
using RollForHonor.Domain.Stats;

namespace RollForHonor.Domain.Tests.Stats;

/// <summary>Verifies immutable stat inputs, deterministic stacking and attribute-specific limits.</summary>
public sealed class StatCalculatorTests
{
    /// <summary>Flat values precede additive increases and independent multipliers.</summary>
    [Fact]
    public void FlatIncreasedAndMoreUseSeparateStages()
    {
        var key = new StatKey(StatType.MaxHealth);
        var bases = new StatBlock([new(key, 100)]);
        StatModifier[] modifiers =
        [
            new(key, StatOperation.Flat, 20, "armor"),
            new(key, StatOperation.Increased, 0.2m, "ring"),
            new(key, StatOperation.Increased, 0.3m, "skill"),
            new(key, StatOperation.More, 0.2m, "aura"),
            new(key, StatOperation.More, -0.5m, "curse")
        ];
        var calculator = new StatCalculator();

        var resolved = calculator.Calculate(new(bases, modifiers));
        var reversed = calculator.Calculate(new(bases, modifiers.Reverse()));

        Assert.Equal(108m, resolved.Get(key));
        Assert.Equal(resolved.Get(key), reversed.Get(key));
        Assert.Equal(100m, bases.Get(key));
    }

    /// <summary>Global and typed increases share a pool, while More applies only once per matching source.</summary>
    [Fact]
    public void DamageCoefficientsCombineGlobalAndTypedModifiersBeforeMultiplication()
    {
        var global = new StatKey(StatType.DamageMultiplier);
        var fire = new StatKey(StatType.DamageMultiplier, DamageType.Fire);
        var stats = new StatCalculator().Calculate(new(new StatBlock(),
        [
            new(global, StatOperation.Increased, 0.2m, "weapon"),
            new(fire, StatOperation.Increased, 0.3m, "ring"),
            new(global, StatOperation.More, 0.2m, "aura")
        ]));

        Assert.Equal(1.8m, stats.Get(fire));
        Assert.Equal(1.44m, stats.Get(new(StatType.DamageMultiplier, DamageType.Cold)));
    }

    /// <summary>Weaknesses remain negative, resistance cannot exceed immunity and chance stays within [0,1].</summary>
    [Fact]
    public void LimitsPreserveWeaknessesAndNeutralDefaults()
    {
        var bases = new StatBlock(
        [
            new(new(StatType.Resistance, DamageType.Fire), -0.25m),
            new(new(StatType.Resistance, DamageType.Cold), 3),
            new(new(StatType.CriticalChance), 2),
            new(new(StatType.Armor), -50),
            new(new(StatType.MaxHealth), -50)
        ]);
        var stats = new StatCalculator().Calculate(new(bases));

        Assert.Equal(-0.25m, stats.Get(new(StatType.Resistance, DamageType.Fire)));
        Assert.Equal(1m, stats.Get(new(StatType.Resistance, DamageType.Cold)));
        Assert.Equal(1m, stats.Get(new(StatType.CriticalChance)));
        Assert.Equal(0m, stats.Get(new(StatType.Armor)));
        Assert.Equal(1m, stats.Get(new(StatType.MaxHealth)));
        Assert.Equal(1m, stats.Get(new(StatType.DamageMultiplier, DamageType.Poison)));
    }

    /// <summary>Input lists are copied and invalid keys, operations and duplicate base entries fail explicitly.</summary>
    [Fact]
    public void ProfilesOwnInputsAndRejectAmbiguousDefinitions()
    {
        var key = new StatKey(StatType.Armor);
        var entries = new List<StatModifier> { new(key, StatOperation.Flat, 5, "armor") };
        var profile = new StatProfile(new StatBlock(), entries);
        entries.Clear();

        Assert.Single(profile.Modifiers);
        Assert.Throws<ArgumentException>(() => new StatKey(StatType.Resistance));
        Assert.Throws<ArgumentException>(() => new StatKey(StatType.MaxHealth, DamageType.Fire));
        Assert.Throws<ArgumentException>(() => new StatBlock([new(key, 1), new(key, 2)]));
        Assert.Throws<ArgumentException>(() => new StatProfile(new StatBlock(), new StatModifier[] { null! }));
        Assert.Throws<ArgumentOutOfRangeException>(() => new StatModifier(key, StatOperation.More, -2, "bad"));
        Assert.Throws<ArgumentException>(() => new StatModifier(new(StatType.DamageMultiplier), StatOperation.Flat, 1, "bad"));
    }

    /// <summary>Complete reductions never create negative damage or invalid HP maxima.</summary>
    [Fact]
    public void ExcessiveReductionsClampAtAttributeBoundaries()
    {
        var key = new StatKey(StatType.MovementSpeed);
        var stats = new StatCalculator().Calculate(new(new StatBlock([new(key, 6)]),
            [new(key, StatOperation.Increased, -3, "root")]));

        Assert.Equal(0m, stats.Get(key));
    }

    /// <summary>Invalid negative base coefficients cannot cancel each other into a positive outgoing multiplier.</summary>
    [Fact]
    public void NegativeBaseDamageCoefficientsRemainZero()
    {
        var bases = new StatBlock(
        [
            new(new(StatType.DamageMultiplier), -1),
            new(new(StatType.DamageMultiplier, DamageType.Fire), -1)
        ]);
        var resolved = new StatCalculator().Calculate(new(bases));

        Assert.Equal(0m, resolved.Get(new(StatType.DamageMultiplier, DamageType.Fire)));
    }

}
