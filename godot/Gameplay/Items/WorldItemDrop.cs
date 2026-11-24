using System;
using Godot;
using RollForHonor.Domain.Items.Models;
using RollForHonor.Godot.UI.Inventory;
using RollForHonor.Godot.UI.Localization;

namespace RollForHonor.Godot.Gameplay.Items;

/// <summary>Displays one captured world item until an authoritative pickup succeeds.</summary>
public partial class WorldItemDrop : Node3D
{
    /// <summary>Gets the immutable item supplied before tree entry.</summary>
    public ItemInstance Item { get; private set; }

    /// <summary>Gets whether pickup succeeded; repeated requests cannot grant another copy.</summary>
    public bool Collected { get; private set; }

    /// <inheritdoc />
    public override void _Ready()
    {
        if (Item is null)
        {
            throw new InvalidOperationException("Configure the captured item before tree entry.");
        }

        var color = ItemPresentation.RarityColor(Item.Rarity);
        AddChild(new MeshInstance3D
        {
            Position = new Vector3(0, 0.25f, 0),
            Mesh = new BoxMesh { Size = new Vector3(0.55f, 0.35f, 0.55f) },
            MaterialOverride = new StandardMaterial3D { AlbedoColor = color, EmissionEnabled = true, Emission = color * 0.4f }
        });
        var label = new Label3D
        {
            Name = "ItemName",
            Position = Vector3.Up * 0.85f,
            Text = L.Text(Item.Base.Name),
            Billboard = BaseMaterial3D.BillboardModeEnum.Enabled,
            FontSize = 30,
            PixelSize = 0.006f,
            Modulate = color
        };
        AddChild(label);
        L.OnChanged(label, () => label.Text = L.Text(Item.Base.Name));
    }

    /// <summary>Captures a previously rolled drop without granting ownership.</summary>
    /// <param name="item">The unowned immutable instance.</param>
    /// <exception cref="ArgumentNullException">The item is null.</exception>
    /// <exception cref="InvalidOperationException">The drop has already entered the tree.</exception>
    public void Configure(ItemInstance item)
    {
        ArgumentNullException.ThrowIfNull(item);

        if (IsInsideTree())
        {
            throw new InvalidOperationException("Configure loot before tree entry.");
        }

        Item = item;
    }

    /// <summary>Removes the world representation only after ownership publication succeeds.</summary>
    /// <param name="receive">The capacity-checked ownership operation.</param>
    /// <returns>True on the first successful pickup; false on rejection or an already collected drop.</returns>
    /// <exception cref="ArgumentNullException">The operation is null.</exception>
    public bool TryCollect(Func<ItemInstance, bool> receive)
    {
        ArgumentNullException.ThrowIfNull(receive);

        if (Collected || !receive(Item))
        {
            return false;
        }

        Collected = true;
        Hide();
        QueueFree();

        return true;
    }
}
