#nullable enable

using System.Collections.Generic;
using AshwoodCounty.Items;
using Godot;

namespace AshwoodCounty.Resources;

/// <summary>
/// Settlement-wide physical item storage: the "survivor inventory &lt;-&gt;
/// settlement storage" side of the loop. Deliberately simple; a flat
/// id-&gt;quantity table, not a zoned storage system.
///
/// Depositing an item follows the authoritative resource relationship (see
/// docs/item_resource_relationship.md): if the item's ItemDefinition carries
/// a ResourceRelationship, depositing it converts the item into that amount
/// of the existing bulk settlement resource (Food/Materials/Medicine) rather
/// than occupying a slot here; this is how consumable supplies (canned food,
/// bandages, scrap) keep contributing to the existing Wood/Food/Materials/
/// Medicine economy without double-accounting. Items with no relationship
/// (tools, weapons, equipment) remain real, distinct stored items.
/// </summary>
public partial class SettlementItemStorage : Node
{
    public const string GroupName = "settlement_item_storage";

    private readonly Dictionary<string, int> _items = [];
    private SettlementInventory? _inventory;

    public override void _Ready()
    {
        AddToGroup(GroupName);
    }

    public IReadOnlyDictionary<string, int> Items => _items;

    public int GetQuantity(string itemId) => _items.GetValueOrDefault(itemId);

    public void Deposit(string itemId, int quantity)
    {
        if (quantity <= 0 || !ItemCatalog.TryGet(itemId, out ItemDefinition definition))
        {
            return;
        }

        if (definition.ResourceRelationship is ItemResourceRelationship relationship)
        {
            _inventory ??= GetTree().GetFirstNodeInGroup(SettlementInventory.GroupName) as SettlementInventory;
            _inventory?.Add(relationship.Type, relationship.Amount * quantity);
            return;
        }

        _items[itemId] = GetQuantity(itemId) + quantity;
    }

    public bool TryWithdraw(string itemId, int quantity)
    {
        if (quantity <= 0 || GetQuantity(itemId) < quantity)
        {
            return false;
        }

        int remaining = GetQuantity(itemId) - quantity;
        if (remaining <= 0) _items.Remove(itemId);
        else _items[itemId] = remaining;
        return true;
    }
}
