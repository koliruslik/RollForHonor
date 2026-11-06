using RollForHonor.Domain.Combat.State.Models;
using RollForHonor.Domain.Combat.Damage.Models;

namespace RollForHonor.Domain.Tests.Combat.State;

public sealed class CombatStateValidationTests
{
    [Fact]
    public void CombatantSnapshot_WhenResistanceExceedsOne_RejectsAtConstruction()
    {
        Assert.Throws<ArgumentException>(() => Snapshot([new ResistanceValue(DamageType.Fire, 1.01m)]));
    }

    [Fact]
    public void CombatantSnapshot_WhenResistanceTypesAreDuplicated_RejectsAtConstruction()
    {
        Assert.Throws<ArgumentException>(() => Snapshot([
            new ResistanceValue(DamageType.Fire, 0.25m),
            new ResistanceValue(DamageType.Fire, 0.5m)
        ]));
    }

    [Fact]
    public void CombatantSnapshot_AcceptsFullResistanceAndNegativeWeakness()
    {
        var snapshot = Snapshot([
            new ResistanceValue(DamageType.Fire, 1m),
            new ResistanceValue(DamageType.Cold, -0.5m)
        ]);

        Assert.Equal(2, snapshot.Resistances.Count);
    }

    private static CombatantSnapshot Snapshot(IReadOnlyList<ResistanceValue> resistances) =>
        new(new CombatantId(Guid.NewGuid()), 50, 100, 0, 0, resistances, []);

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
