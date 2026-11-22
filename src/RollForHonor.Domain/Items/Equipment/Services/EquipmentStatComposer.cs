using RollForHonor.Domain.Items.Equipment.Models;
using RollForHonor.Domain.Items.Services;
using RollForHonor.Domain.Stats;

namespace RollForHonor.Domain.Items.Equipment.Services;

/// <summary>Rebuilds the complete equipment contribution without filtering or altering other modifier sources.</summary>
public sealed class EquipmentStatComposer
{
    /// <summary>Combines a separately owned non-equipment profile with exactly one projection of every occupied slot.</summary>
    /// <param name="nonEquipmentProfile">Base values and modifiers from all sources except equipment.</param>
    /// <param name="loadout">The current immutable occupied slots.</param>
    /// <returns>A complete profile; an empty loadout returns the original non-equipment profile.</returns>
    /// <exception cref="ArgumentNullException">An input is null.</exception>
    public StatProfile Compose(StatProfile nonEquipmentProfile, EquipmentLoadout loadout)
    {
        ArgumentNullException.ThrowIfNull(nonEquipmentProfile);
        ArgumentNullException.ThrowIfNull(loadout);

        if (loadout.Slots.Count == 0)
        {
            return nonEquipmentProfile;
        }

        var projector = new ItemModifierProjector();
        var itemModifiers = loadout.Slots
            .OrderBy(pair => pair.Key)
            .SelectMany(pair => projector.Project(pair.Value));
        var modifiers = nonEquipmentProfile.Modifiers.Concat(itemModifiers);

        return new StatProfile(nonEquipmentProfile.BaseValues, modifiers);
    }
}
