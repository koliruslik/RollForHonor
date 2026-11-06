using RollForHonor.Domain.Combat.Effects.Models;
using RollForHonor.Domain.Combat.Effects.Services;
using RollForHonor.Domain.Combat.State.Changes;
using RollForHonor.Domain.Combat.State.Models;

namespace RollForHonor.Domain.Tests.Combat.Effects;

public sealed class EffectContractTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public void InstanceIdentity_IsRepeatable_AndChangesWithEveryKeyComponent(int changedComponent)
    {
        var factory = new ResolutionEffectInstanceIdFactory();
        var resolution = Guid.NewGuid();
        var recipient = new CombatantId(Guid.NewGuid());
        var definition = new EffectDefinitionId(Guid.NewGuid());
        var original = factory.Create(resolution, recipient, definition, 0);

        var changed = factory.Create(changedComponent == 0 ? Guid.NewGuid() : resolution,
            changedComponent == 1 ? new CombatantId(Guid.NewGuid()) : recipient,
            changedComponent == 2 ? new EffectDefinitionId(Guid.NewGuid()) : definition,
            changedComponent == 3 ? 1 : 0);

        Assert.Equal(original, factory.Create(resolution, recipient, definition, 0));
        Assert.NotEqual(original, changed);
        Assert.NotEqual(Guid.Empty, original.Value);
    }

    [Fact]
    public void InstanceIdentity_RejectsAnEmptyResolutionAndNegativeOccurrence()
    {
        var factory = new ResolutionEffectInstanceIdFactory();
        var recipient = new CombatantId(Guid.NewGuid());
        var definition = new EffectDefinitionId(Guid.NewGuid());

        Assert.Throws<ArgumentException>(() => factory.Create(Guid.Empty, recipient, definition, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => factory.Create(Guid.NewGuid(), recipient, definition, -1));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    public void Resolution_RejectsNullItemsAndPrematureHealthOrDefeatChanges(int invalidKind)
    {
        var recipient = new CombatantId(Guid.NewGuid());

        var error = Assert.Throws<ArgumentException>(() => _ = invalidKind switch
        {
            0 => new EffectResolution([null!], [], []),
            1 => new EffectResolution([], [null!], []),
            2 => new EffectResolution([], [], [null!]),
            3 => new EffectResolution([], [], [new HealthChanged(recipient, 50, 40)]),
            _ => new EffectResolution([], [], [new CombatantDefeated(recipient)])
        });

        Assert.Equal(invalidKind switch
        {
            0 => "effects",
            1 => "healthAdjustments",
            _ => "stateChanges"
        }, error.ParamName);
    }
}
