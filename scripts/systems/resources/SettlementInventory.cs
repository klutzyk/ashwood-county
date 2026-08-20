using System.Collections.Generic;
using Godot;

namespace AshwoodCounty.Resources;

public partial class SettlementInventory : Node
{
    public const string GroupName = "settlement_inventory";

    private readonly Dictionary<ResourceType, int> _amounts = [];

    [Export] public bool DevUnlimitedResources { get; set; } = false;

    public override void _Ready()
    {
        AddToGroup(GroupName);
    }

    public int GetAmount(ResourceType resourceType)
    {
        return _amounts.GetValueOrDefault(resourceType);
    }

    public bool CanAfford(ResourceType resourceType, int amount)
    {
        return amount >= 0 && (DevUnlimitedResources || GetAmount(resourceType) >= amount);
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
        if (!CanAfford(resourceType, amount))
        {
            return false;
        }

        if (DevUnlimitedResources)
        {
            return true;
        }

        _amounts[resourceType] = GetAmount(resourceType) - amount;
        return true;
    }
}
