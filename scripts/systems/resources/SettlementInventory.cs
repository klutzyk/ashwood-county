using System.Collections.Generic;
using Godot;

namespace AshwoodCounty.Resources;

public partial class SettlementInventory : Node
{
    public const string GroupName = "settlement_inventory";

    private readonly Dictionary<ResourceType, int> _amounts = [];

    public override void _Ready()
    {
        AddToGroup(GroupName);
    }

    public int GetAmount(ResourceType resourceType)
    {
        return _amounts.GetValueOrDefault(resourceType);
    }

    public void Add(ResourceType resourceType, int amount)
    {
        if (amount <= 0)
        {
            return;
        }

        _amounts[resourceType] = GetAmount(resourceType) + amount;
    }

    public bool TrySpend(ResourceType resourceType, int amount)
    {
        if (amount < 0 || GetAmount(resourceType) < amount)
        {
            return false;
        }

        _amounts[resourceType] = GetAmount(resourceType) - amount;
        return true;
    }
}
