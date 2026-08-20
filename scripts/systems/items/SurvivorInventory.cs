#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

namespace AshwoodCounty.Items;

/// <summary>
/// A survivor's physical, persistent inventory: carried item stacks plus
/// equipped weapon/backpack. Plain C# state (no Node references) so it stays
/// safe to serialize later, mirroring how <see cref="Units.SurvivorProfile"/>
/// is already a plain class field on Survivor rather than a child node.
/// Capacity is weight-based: BaseCapacityKg + the equipped backpack's bonus.
/// </summary>
public sealed class SurvivorInventory
{
    private readonly List<ItemStack> _items = [];
    private readonly Dictionary<EquipmentSlot, string?> _equipped = new()
    {
        [EquipmentSlot.Weapon] = null,
        [EquipmentSlot.Backpack] = null,
        [EquipmentSlot.Light] = null,
    };

    public float BaseCapacityKg { get; set; } = 20f;

    public IReadOnlyList<ItemStack> Items => _items;
    public string? EquippedWeaponId => _equipped[EquipmentSlot.Weapon];
    public string? EquippedBackpackId => _equipped[EquipmentSlot.Backpack];
    public string? EquippedLightId => _equipped[EquipmentSlot.Light];
    public string? Equipped(EquipmentSlot slot) => slot == EquipmentSlot.None ? null : _equipped[slot];

    public float TotalCapacityKg => BaseCapacityKg + BonusFor(EquippedBackpackId);

    public float CurrentWeightKg
    {
        get
        {
            float total = _items.Sum(stack => WeightOf(stack.ItemId) * stack.Quantity);
            total += WeightOf(EquippedWeaponId);
            total += WeightOf(EquippedBackpackId);
            total += WeightOf(EquippedLightId);
            return total;
        }
    }

    public float RemainingCapacityKg => Mathf.Max(0f, TotalCapacityKg - CurrentWeightKg);

    public int GetQuantity(string itemId) => _items.Where(stack => stack.ItemId == itemId).Sum(stack => stack.Quantity);
    public bool Has(string itemId, int quantity = 1) => GetQuantity(itemId) >= quantity;

    /// <summary>
    /// Adds up to <paramref name="quantity"/> of an item, limited by remaining
    /// carry weight. Returns the amount actually added (may be less than
    /// requested, and may be zero); callers that need "all or nothing" should
    /// check the return value against the request themselves.
    /// </summary>
    public int TryAdd(string itemId, int quantity)
    {
        if (quantity <= 0 || !ItemCatalog.TryGet(itemId, out ItemDefinition definition)) return 0;
        int maxByWeight = definition.Weight <= 0f ? quantity : (int)Mathf.Floor(RemainingCapacityKg / definition.Weight);
        int toAdd = Mathf.Min(quantity, Mathf.Max(0, maxByWeight));
        if (toAdd <= 0) return 0;
        AddIgnoringCapacity(itemId, toAdd, definition.MaxStackSize);
        return toAdd;
    }

    public bool TryRemove(string itemId, int quantity)
    {
        if (quantity <= 0 || GetQuantity(itemId) < quantity) return false;
        int remaining = quantity;
        for (int i = _items.Count - 1; i >= 0 && remaining > 0; i--)
        {
            if (_items[i].ItemId != itemId) continue;
            int take = Mathf.Min(_items[i].Quantity, remaining);
            int left = _items[i].Quantity - take;
            if (left <= 0) _items.RemoveAt(i);
            else _items[i] = _items[i] with { Quantity = left };
            remaining -= take;
        }
        return remaining == 0;
    }

    public bool Equip(string itemId)
    {
        if (!ItemCatalog.TryGet(itemId, out ItemDefinition definition) || !definition.Equippable || definition.EquipmentSlot == EquipmentSlot.None) return false;
        if (!Has(itemId)) return false;
        if (!TryRemove(itemId, 1)) return false;

        string? previous = _equipped[definition.EquipmentSlot];
        if (previous is not null) AddIgnoringCapacity(previous, 1, ItemCatalog.TryGet(previous, out ItemDefinition previousDefinition) ? previousDefinition.MaxStackSize : 1);
        _equipped[definition.EquipmentSlot] = itemId;
        return true;
    }

    public bool Unequip(EquipmentSlot slot)
    {
        if (slot == EquipmentSlot.None || _equipped[slot] is not string itemId) return false;
        _equipped[slot] = null;
        AddIgnoringCapacity(itemId, 1, ItemCatalog.TryGet(itemId, out ItemDefinition definition) ? definition.MaxStackSize : 1);
        return true;
    }

    private void AddIgnoringCapacity(string itemId, int quantity, int maxStackSize)
    {
        int toAdd = quantity;
        for (int i = 0; i < _items.Count && toAdd > 0; i++)
        {
            if (_items[i].ItemId != itemId) continue;
            int space = maxStackSize - _items[i].Quantity;
            if (space <= 0) continue;
            int move = Mathf.Min(space, toAdd);
            _items[i] = _items[i] with { Quantity = _items[i].Quantity + move };
            toAdd -= move;
        }
        while (toAdd > 0)
        {
            int stackAmount = Mathf.Min(toAdd, Mathf.Max(1, maxStackSize));
            _items.Add(new ItemStack(itemId, stackAmount));
            toAdd -= stackAmount;
        }
    }

    private static float WeightOf(string? itemId) => itemId is not null && ItemCatalog.TryGet(itemId, out ItemDefinition definition) ? definition.Weight : 0f;
    private static float BonusFor(string? itemId) => itemId is not null && ItemCatalog.TryGet(itemId, out ItemDefinition definition) ? definition.CapacityBonusKg : 0f;
}
