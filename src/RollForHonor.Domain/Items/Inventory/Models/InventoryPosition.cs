namespace RollForHonor.Domain.Items.Inventory.Models;

/// <summary>A candidate top-left cell, validated against a particular grid by its placement service.</summary>
/// <param name="Column">The zero-based column.</param>
/// <param name="Row">The zero-based row.</param>
public readonly record struct InventoryPosition(int Column, int Row);
