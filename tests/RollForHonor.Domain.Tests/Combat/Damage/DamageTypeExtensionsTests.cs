using RollForHonor.Domain.Combat.Damage.Extensions;
using RollForHonor.Domain.Combat.Damage.Models;

namespace RollForHonor.Domain.Tests.Combat.Damage;

public sealed class DamageTypeExtensionsTests
{
    [Theory]
    [InlineData(DamageType.Piercing, true)]
    [InlineData(DamageType.Bludgeoning, true)]
    [InlineData(DamageType.Slashing, true)]
    [InlineData(DamageType.Fire, false)]
    [InlineData(DamageType.Cold, false)]
    [InlineData(DamageType.Lightning, false)]
    [InlineData(DamageType.Poison, false)]
    [InlineData((DamageType)(-1), false)]
    public void IsPhysical_ClassifiesDamageTypes(DamageType type, bool expected)
    {
        Assert.Equal(expected, type.IsPhysical());
    }
}
