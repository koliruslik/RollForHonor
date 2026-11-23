using RollForHonor.Domain.Combat.Damage.Models;
using RollForHonor.Domain.Items.Models;

namespace RollForHonor.Domain.Tests.Items;

/// <summary>Verifies intrinsic weapon content remains valid and independent of mutable authoring lists.</summary>
public sealed class WeaponPropertiesTests
{
    /// <summary>Weapon properties retain a copy of authored damage components.</summary>
    [Fact]
    public void WeaponRetainsCopiedDamageAndItsTiming()
    {
        var damage = new List<DamageComponent> { new(new(1, 6), 3, DamageType.Piercing) };
        var weapon = new WeaponProperties(4, 10, damage);
        damage.Clear();
        Assert.Single(weapon.Damage);
        Assert.Equal(4, weapon.AttacksPerSecond);
        Assert.Equal(10, weapon.ManaCost);
        var definition = new ItemDefinition("bow", "Bow", ["weapon"], weapon: weapon);
        Assert.Same(weapon, definition.Weapon);
        Assert.Throws<ArgumentOutOfRangeException>(() => new WeaponProperties(0, 0, weapon.Damage));
        Assert.Throws<ArgumentOutOfRangeException>(() => new WeaponProperties(1, -1, weapon.Damage));
        Assert.Throws<ArgumentException>(() => new WeaponProperties(1, 0, []));
    }
}
