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
}
