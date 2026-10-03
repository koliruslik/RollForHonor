using RollForHonor.Domain.Combat.State;

namespace RollForHonor.Domain.Tests.Combat;

public sealed class CombatStateValidationTests
{
    [Fact]
    public void CombatantId_WhenGuidIsEmpty_Throws()
    {
        Assert.Throws<ArgumentException>(
            () => new CombatantId(Guid.Empty));
    }

    [Fact]
    public void CombatantSnapshot_WhenHealthExceedsMaximum_Throws()
    {
        var id = new CombatantId(Guid.NewGuid());

        Assert.Throws<ArgumentOutOfRangeException>(
            () => new CombatantSnapshot(
                id,
                health: 11,
                maxHealth: 10,
                armor: 0,
                evasion: 0,
                resistances: [],
                effects: []));
    }

    [Theory]
    [InlineData(0, true)]
    [InlineData(1, false)]
    public void CombatantSnapshot_IsDefeated_ReflectsWhetherHealthIsZero(
        int health,
        bool expected)
    {
        var snapshot = new CombatantSnapshot(
            new CombatantId(Guid.NewGuid()),
            health,
            maxHealth: 10,
            armor: 0,
            evasion: 0,
            resistances: [],
            effects: []);

        Assert.Equal(expected, snapshot.IsDefeated);
    }
}
