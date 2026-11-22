namespace RollForHonor.Domain.Items.Equipment.Models;

/// <summary>Identifies the seven independently occupied equipment slots in the minimal loadout.</summary>
public enum EquipmentSlot
{
    /// <summary>The single weapon slot; handedness is not modelled yet.</summary>
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
    RingRight
}
