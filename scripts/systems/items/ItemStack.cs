namespace AshwoodCounty.Items;

/// <summary>
/// A quantity of a single item kind. Deliberately just data (an id + a count)
/// so it survives being copied between a container's revealed loot, a
/// survivor's inventory, and settlement storage without ever holding a Node
/// reference, so it stays safe to persist later without special-casing.
/// </summary>
public readonly record struct ItemStack(string ItemId, int Quantity);
