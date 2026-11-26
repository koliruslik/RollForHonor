using System.Linq;
using RollForHonor.Godot.Arena;
using RollForHonor.Godot.Content.Items;
using RollForHonor.Godot.UI.Hud;

namespace RollForHonor.Godot.Tests;

/// <summary>Supplies known affix values for presentation tests independently of evolving live balance.</summary>
internal static class ArenaItemFixtures
{
    /// <summary>Replaces the generated starting kit with explicit authored inspection fixtures.</summary>
    /// <param name="arena">An initialized, unpaused arena with no equipped items.</param>
    public static void UseAuthoredKit(GreyboxArena arena)
    {
        var state = arena.GetParent<GameBootstrap>().CombatState;

        if (arena.NeedsStarterWeapon)
        {
            arena.SelectStarterWeapon("dexterity-weapon-travel");
            new RollForHonor.Application.Equipment.Services.EquipmentService(state)
                .UnequipItem(arena.Player.CombatantId, RollForHonor.Domain.Items.Equipment.Models.EquipmentSlot.Weapon);
            arena.GetNode<ArenaHud>("Presentation")._Process(0);
        }

        foreach (var item in state.ReadEquipment(arena.Player.CombatantId).OwnedItems.Values.ToArray())
        {
            arena.DiscardItem(item.Id);
        }

        foreach (var drop in arena.Drops)
        {
            drop.Free();
        }

        foreach (var item in StarterItemCatalog.Create(arena.Player.CombatantId))
        {
            state.RegisterOwnedItem(arena.Player.CombatantId, item);
        }

        arena.GetNode<ArenaHud>("Presentation")._Process(0);
    }
}
