#nullable enable

using System.Linq;
using AshwoodCounty.Items;
using AshwoodCounty.Resources;
using AshwoodCounty.UI;
using AshwoodCounty.Units;
using Godot;

namespace AshwoodCounty.Systems;

/// <summary>
/// Applies the authored starting day once, after every runtime node is ready.
/// The base World scene remains a lightweight arrangement of systems; the
/// survival tuning here turns that arrangement into an actual opening scenario.
/// </summary>
public partial class StartingScenario : Node
{
    public const string GroupName = "starting_scenario";

    private bool _applied;

    public override void _Ready()
    {
        AddToGroup(GroupName);
        Callable.From(Apply).CallDeferred();
    }

    private void Apply()
    {
        if (_applied)
        {
            return;
        }

        _applied = true;

        SettlementInventory inventory = GetTree().GetFirstNodeInGroup(SettlementInventory.GroupName) as SettlementInventory
            ?? throw new System.InvalidOperationException("SettlementInventory missing before starting scenario.");
        inventory.DevUnlimitedResources = false;
        foreach ((ResourceType type, int amount) in SurvivalTuning.StartingStock)
        {
            inventory.Add(type, amount);
        }

        Survivor[] survivors = GetTree().GetNodesInGroup(Survivor.GroupName).OfType<Survivor>()
            .Where(survivor => survivor.IsAlive)
            .OrderBy(survivor => survivor.Name.ToString())
            .ToArray();
        for (int index = 0; index < survivors.Length; index++)
        {
            survivors[index].Hunger = SurvivalTuning.StartingHunger;
            survivors[index].Energy = SurvivalTuning.StartingEnergy;
            survivors[index].Morale = SurvivalTuning.StartingMorale;
        }

        foreach ((int survivorIndex, string itemId, int quantity, bool equip) in SurvivalTuning.StartingSurvivorGear)
        {
            if (survivorIndex < 0 || survivorIndex >= survivors.Length)
            {
                continue;
            }

            Survivor survivor = survivors[survivorIndex];
            if (survivor.Inventory.TryAdd(itemId, quantity) <= 0)
            {
                continue;
            }

            if (equip)
            {
                survivor.EquipItem(itemId);
            }
        }

        CreateFoodCache();

        if (GetTree().GetFirstNodeInGroup(GameClock.GroupName) is GameClock clock)
        {
            clock.SetTotalMinutes(SurvivalTuning.StartingGameMinutes);
        }

        (GetTree().GetFirstNodeInGroup(GameHud.GroupName) as GameHud)?.Notify(
            "DAY 1  MORNING\nSecure shelter, food, and useful supplies before dark.");
    }

    /// <summary>
    /// A small, non-autonomous food cache near the camp. Unlike the two
    /// pre-designated world caches, this one waits for the player to mark it,
    /// which teaches scavenge designation while still providing an obvious
    /// nearby food opportunity.
    /// </summary>
    private void CreateFoodCache()
    {
        Node2D? objects = GetNodeOrNull<Node2D>("../World/Objects");
        if (objects is null)
        {
            return;
        }

        PackedScene packed = GD.Load<PackedScene>("res://scenes/world/ScavengeSource.tscn");
        ScavengeSource cache = packed.Instantiate<ScavengeSource>();
        cache.Name = "AbandonedCooler";
        cache.DisplayName = "Abandoned Cooler";
        cache.GridPosition = new Vector2(213f, 163f);
        cache.LootType = ResourceType.Food;
        cache.StartingAmount = 8;
        cache.SearchDuration = 3.5f;
        cache.DesignatedAtStart = false;
        objects.AddChild(cache);
    }
}
