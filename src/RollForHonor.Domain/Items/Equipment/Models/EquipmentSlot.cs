namespace RollForHonor.Domain.Items.Equipment.Models;

/// <summary>Identifies equipment slots, including a main weapon and a compatible offhand.</summary>
public enum EquipmentSlot
{
    /// <summary>The main-hand weapon slot.</summary>
    Weapon,

    /// <summary>The torso armor slot.</summary>
    BodyArmor,

    /// <summary>The head armor slot.</summary>
    Helmet,

    /// <summary>The footwear slot.</summary>
    Boots,

    /// <summary>The necklace slot.</summary>
    Amulet,

    /// <summary>The first ring slot.</summary>
    RingLeft,

    /// <summary>The second ring slot, requiring a different owned instance.</summary>
    RingRight,

    /// <summary>The secondary hand, used for a compatible quiver or shield.</summary>
    OffHand
}
