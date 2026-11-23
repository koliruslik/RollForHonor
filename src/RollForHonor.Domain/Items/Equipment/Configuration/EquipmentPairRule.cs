namespace RollForHonor.Domain.Items.Equipment.Configuration;

/// <summary>Defines which main-hand tag permits an offhand item category.</summary>
public sealed record EquipmentPairRule
{
    /// <summary>Gets the offhand category tag, such as quiver or shield.</summary>
    public string OffHandTag { get; }

    /// <summary>Gets the compatible main-hand tag, such as bow or one-handed.</summary>
    public string MainHandTag { get; }

    /// <summary>Gets whether this offhand can remain equipped without a main-hand weapon.</summary>
    public bool AllowsEmptyMainHand { get; }

    /// <summary>Creates an immutable tag-based hand pairing rule.</summary>
    /// <param name="offHandTag">A nonblank ordinal offhand category.</param>
    /// <param name="mainHandTag">A nonblank ordinal main-hand category.</param>
    /// <param name="allowsEmptyMainHand">Whether an empty main hand is also compatible.</param>
    /// <exception cref="ArgumentException">A tag is blank.</exception>
    public EquipmentPairRule(string offHandTag, string mainHandTag, bool allowsEmptyMainHand = false)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(offHandTag);
        ArgumentException.ThrowIfNullOrWhiteSpace(mainHandTag);

        OffHandTag = offHandTag;
        MainHandTag = mainHandTag;
        AllowsEmptyMainHand = allowsEmptyMainHand;
    }
}
