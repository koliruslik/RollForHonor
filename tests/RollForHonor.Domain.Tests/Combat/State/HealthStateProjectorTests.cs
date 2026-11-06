using RollForHonor.Domain.Combat.State.Services;
using RollForHonor.Domain.Combat.State.Models;

namespace RollForHonor.Domain.Tests.Combat.State;

public sealed class HealthStateProjectorTests
{
    private readonly HealthStateProjector _projector = new();

    [Fact]
    public void Project_WhenDamageAndHealingArePresent_AppliesNetChangeAtomically()
    {
        var combatant = CreateCombatant(health: 90, maxHealth: 100);
        var adjustments = new[]
        {
            new HealthAdjustment(combatant.Id, 20),
            new HealthAdjustment(combatant.Id, -15)
        };

        var result = _projector.Project(combatant, adjustments);

        Assert.Equal(90, result.PreviousHealth);
        Assert.Equal(95, result.CurrentHealth);
        Assert.True(result.HasChanged);
        Assert.False(result.BecameDefeated);
    }

    [Fact]
    public void Project_WhenDamageExceedsHealth_ClampsAtZeroAndMarksDefeat()
    {
        var combatant = CreateCombatant(health: 10, maxHealth: 100);
        var adjustments = new[]
        {
            new HealthAdjustment(combatant.Id, -25)
        };

        var result = _projector.Project(combatant, adjustments);

        Assert.Equal(0, result.CurrentHealth);
        Assert.True(result.BecameDefeated);
    }

    [Fact]
    public void Project_WhenHealingExceedsMaximum_ClampsAtMaximumHealth()
    {
        var combatant = CreateCombatant(health: 90, maxHealth: 100);
        var adjustments = new[]
        {
            new HealthAdjustment(combatant.Id, 25)
        };

        var result = _projector.Project(combatant, adjustments);

        Assert.Equal(100, result.CurrentHealth);
        Assert.False(result.BecameDefeated);
    }

    [Fact]
    public void Project_WhenAdjustmentTargetsAnotherCombatant_Throws()
    {
        var combatant = CreateCombatant(health: 50, maxHealth: 100);
        var adjustment = new HealthAdjustment(
            new CombatantId(Guid.NewGuid()),
            -10);

        Assert.Throws<ArgumentException>(
            () => _projector.Project(combatant, new[] { adjustment }));
    }

    [Theory]
    [InlineData(int.MaxValue, int.MaxValue, 100)]
    [InlineData(int.MinValue, int.MinValue, 0)]
    [InlineData(int.MaxValue, int.MinValue, 49)]
    public void Project_WhenCombinedAdjustmentsReachIntegerLimits_UsesWideSumBeforeClamping(
        int first, int second, int expected)
    {
        var combatant = CreateCombatant(health: 50, maxHealth: 100);

        var result = _projector.Project(combatant,
            [new HealthAdjustment(combatant.Id, first), new HealthAdjustment(combatant.Id, second)]);

        Assert.Equal(expected, result.CurrentHealth);
        Assert.Equal(expected == 0, result.BecameDefeated);
        Assert.Equal(50, combatant.Health);
    }

    private static CombatantSnapshot CreateCombatant(int health, int maxHealth)
    {
        return new CombatantSnapshot(
            new CombatantId(Guid.NewGuid()),
            health,
            maxHealth,
            armor: 0,
            evasion: 0,
            resistances: [],
            effects: []);
    }
}
