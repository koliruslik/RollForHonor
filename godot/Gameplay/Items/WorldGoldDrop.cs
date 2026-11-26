using System;
using Godot;
using RollForHonor.Application.Rewards;
using RollForHonor.Domain.Combat.State.Models;
using RollForHonor.Godot.UI.Localization;

namespace RollForHonor.Godot.Gameplay.Items;

/// <summary>Displays one captured gold reward until an atomic wallet collection succeeds.</summary>
public partial class WorldGoldDrop : Node3D
{
    /// <summary>Gets the canonical captured reward configured before tree entry.</summary>
    public GoldReward Reward { get; private set; }

    /// <summary>Gets the stable nearby-loot identity, distinct from item identities.</summary>
    public string LootId => $"gold:{Reward.SourceId.Value:N}";

    /// <summary>Gets whether this world representation has been consumed.</summary>
    public bool Collected { get; private set; }

    /// <inheritdoc />
    public override void _Ready()
    {
        if (Reward is null)
        {
            throw new InvalidOperationException("Configure captured gold before tree entry.");
        }

        var color = new Color("d5b56e");
        AddChild(new MeshInstance3D
        {
            Position = Vector3.Up * 0.15f,
            Mesh = new CylinderMesh { TopRadius = 0.25f, BottomRadius = 0.25f, Height = 0.18f },
            MaterialOverride = new StandardMaterial3D { AlbedoColor = color, Metallic = 0.7f }
        });
        var label = new Label3D
        {
            Name = "GoldAmount",
            Position = Vector3.Up * 0.65f,
            Billboard = BaseMaterial3D.BillboardModeEnum.Enabled,
            FontSize = 30,
            PixelSize = 0.006f,
            Modulate = color
        };
        AddChild(label);
        L.OnChanged(label, () => label.Text = L.Format($"Gold: {Reward.Amount}"));
    }

    /// <summary>Captures the canonical amount without crediting a wallet.</summary>
    /// <param name="reward">The immutable reward already registered in the session.</param>
    /// <exception cref="ArgumentNullException">The reward is null.</exception>
    /// <exception cref="InvalidOperationException">The node has already entered the tree.</exception>
    public void Configure(GoldReward reward)
    {
        ArgumentNullException.ThrowIfNull(reward);

        if (IsInsideTree())
        {
            throw new InvalidOperationException("Configure gold before tree entry.");
        }

        Reward = reward;
    }

    /// <summary>Removes the visible reward only after collection or a confirmed earlier consumption.</summary>
    /// <param name="collect">The atomic wallet operation for the captured source.</param>
    /// <returns>True only for a newly credited wallet; conflicts retain the drop for another player action.</returns>
    /// <exception cref="ArgumentNullException">The callback is null.</exception>
    public bool TryCollect(Func<CombatantId, GoldPickupStatus> collect)
    {
        ArgumentNullException.ThrowIfNull(collect);

        if (Collected)
        {
            return false;
        }

        var status = collect(Reward.SourceId);

        if (status is not (GoldPickupStatus.Collected or GoldPickupStatus.AlreadyCollected))
        {
            return false;
        }

        Collected = true;
        Hide();
        QueueFree();

        return status == GoldPickupStatus.Collected;
    }
}
