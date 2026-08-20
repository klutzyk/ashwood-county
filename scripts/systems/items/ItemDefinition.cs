#nullable enable

using AshwoodCounty.Resources;

namespace AshwoodCounty.Items;

public enum ItemCategory { Food, Medical, Tool, Material, MeleeWeapon, Equipment, Misc }
public enum EquipmentSlot { None, Weapon, Backpack, Light }

/// <summary>
/// How a physical item is accounted for once deposited into settlement item
/// storage. When set, depositing the item converts it into this amount of a
/// bulk settlement resource instead of remaining a stored item (see
/// docs/item_resource_relationship.md). Null means the item stays a real,
/// distinct entry in settlement storage (tools, weapons, equipment).
/// </summary>
public sealed record ItemResourceRelationship(ResourceType Type, int Amount);

/// <summary>
/// Immutable catalog entry describing one kind of item. Runtime carried/stored
/// quantities live separately in <see cref="ItemStack"/>; this type only ever
/// describes the *definition*, never a particular survivor's or container's
/// holdings, so it is safe to share a single instance everywhere.
/// </summary>
public sealed record ItemDefinition(
    string Id,
    string DisplayName,
    string Description,
    ItemCategory Category,
    float Weight,
    int MaxStackSize,
    string[] Tags,
    bool Usable = false,
    bool Equippable = false,
    EquipmentSlot EquipmentSlot = EquipmentSlot.None,
    ItemResourceRelationship? ResourceRelationship = null,
    float NutritionValue = 0f,
    float HealValue = 0f,
    float CapacityBonusKg = 0f,
    float DamageValue = 0f)
{
    private static readonly System.Collections.Generic.Dictionary<ItemCategory, string> CategoryFolders = new()
    {
        [ItemCategory.Food] = "food",
        [ItemCategory.Medical] = "medical",
        [ItemCategory.Tool] = "tools",
        [ItemCategory.Material] = "materials",
        [ItemCategory.MeleeWeapon] = "melee",
        [ItemCategory.Equipment] = "equipment",
        [ItemCategory.Misc] = "misc",
    };

    public string IconPath => $"res://assets/art/items/{CategoryFolders[Category]}/{Id}.png";
    public bool HasTag(string tag) => System.Array.IndexOf(Tags, tag) >= 0;
}
