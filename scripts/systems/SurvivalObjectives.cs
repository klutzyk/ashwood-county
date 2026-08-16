#nullable enable

using System.Collections.Generic;
using System.Linq;
using AshwoodCounty.Buildings;
using AshwoodCounty.Buildings.Interiors;
using AshwoodCounty.Resources;
using AshwoodCounty.Threats;
using AshwoodCounty.UI;
using AshwoodCounty.Units;
using Godot;

namespace AshwoodCounty.Systems;

/// <summary>
/// A restrained contextual survival director. It does not own quests or
/// dialogue; it samples the existing world state on a slow tick and derives one
/// current survival priority plus one-time milestone notifications. The first
/// day naturally moves through shelter, food, scavenging, dusk, night, and dawn
/// without a hard-coded mission list.
/// </summary>
public partial class SurvivalObjectives : Node
{
    public const string GroupName = "survival_objectives";

    private const double EvaluationInterval = 0.5;
    private const float DangerRadius = 5.5f;
    private const double DangerWarningCooldown = 20.0;

    private static SurvivalObjectives? Current;

    private readonly HashSet<string> _completedMilestones = [];
    private GameClock _clock = null!;
    private SettlementInventory _inventory = null!;
    private double _elapsed;
    private double _dangerWarningElapsed;
    private string _currentPriority = "ASSESS THE CAMP\nCheck survivors, supplies, and nearby threats.";

    public static SurvivalObjectives? Active
        => Current is not null && GodotObject.IsInstanceValid(Current) ? Current : null;

    public string CurrentPriority => _currentPriority;
    public IReadOnlyCollection<string> CompletedMilestones => _completedMilestones;

    public override void _Ready()
    {
        Current = this;
        AddToGroup(GroupName);
        _clock = GetTree().GetFirstNodeInGroup(GameClock.GroupName) as GameClock
            ?? throw new System.InvalidOperationException("GameClock missing for SurvivalObjectives.");
        _inventory = GetTree().GetFirstNodeInGroup(SettlementInventory.GroupName) as SettlementInventory
            ?? throw new System.InvalidOperationException("SettlementInventory missing for SurvivalObjectives.");
    }

    public override void _Process(double delta)
    {
        _elapsed += delta;
        if (_elapsed >= EvaluationInterval)
        {
            _elapsed = 0;
            Evaluate();
            MonitorDanger();
        }
    }

    public bool HasCompleted(string milestoneId) => _completedMilestones.Contains(milestoneId);

    private void Evaluate()
    {
        bool shelter = HasRestShelter();
        bool foodGoal = _inventory.GetAmount(ResourceType.Food) >= SurvivalTuning.FoodStockpileGoal;
        InteriorBuildingRuntime? home = FindAbandonedHome();
        bool homeSearched = home is not null && (home.SearchedContainerCount > 0 || home.DiscoveredRoomCount > 0);
        TimeOfDay phase = SurvivalCycle.Active?.Phase ?? TimeOfDay.Day;
        int day = _clock.Day;
        bool firstNightSurvived = day >= 2 && phase is TimeOfDay.Dawn or TimeOfDay.Day;

        if (shelter)
        {
            Mark("shelter_secured", "SHELTER SECURED\nSurvivors now have a safe place to rest.");
        }

        if (foodGoal)
        {
            Mark("food_secured", $"FOOD SECURED\n{_inventory.GetAmount(ResourceType.Food)} food stored for the group.");
        }

        if (homeSearched)
        {
            Mark("home_searched", "SUPPLIES FOUND\nThe abandoned home is worth more than a glance.");
        }

        if (firstNightSurvived)
        {
            Mark("first_night_survived", $"DAY {day}\nYou survived the first night.");
        }

        _currentPriority = DeterminePriority(shelter, foodGoal, homeSearched, home, phase, day);
    }

    private void MonitorDanger()
    {
        _dangerWarningElapsed = Mathf.Max(0, _dangerWarningElapsed - EvaluationInterval);
        if (_dangerWarningElapsed > 0)
        {
            return;
        }

        if (AnyThreatened())
        {
            Notify("INFECTED NEARBY\nKeep your distance or order survivors to defend themselves.");
            _dangerWarningElapsed = DangerWarningCooldown;
        }
    }

    private string DeterminePriority(
        bool shelter,
        bool foodGoal,
        bool homeSearched,
        InteriorBuildingRuntime? home,
        TimeOfDay phase,
        int day)
    {
        if (phase == TimeOfDay.Night)
        {
            return "NIGHT\nKeep survivors sheltered and resting. Use lights outdoors.";
        }

        if (phase == TimeOfDay.Dusk)
        {
            return "DUSK\nFinish nearby work and return to shelter before dark.";
        }

        if (!shelter)
        {
            return "BUILD SHELTER\nChop trees (WORK > CHOP) and place a Shelter before dark.";
        }

        if (!foodGoal)
        {
            return "GATHER FOOD\nUse WORK > FORAGE/SCAVENGE or search the abandoned home.";
        }

        if (!homeSearched)
        {
            return "SEARCH THE ABANDONED HOME\nInvestigate the house east of camp.";
        }

        return day >= 2
            ? NextDayPriority(foodGoal, home)
            : "IMPROVE THE SETTLEMENT\nBuild storage, gather materials, or prepare for tomorrow.";
    }

    private string NextDayPriority(bool foodGoal, InteriorBuildingRuntime? home)
    {
        if (!foodGoal)
        {
            return "FOOD RUNNING LOW\nForage or scavenge more before supplies run out.";
        }

        if (_inventory.GetAmount(ResourceType.Medicine) < SurvivalTuning.MedicineStockpileGoal)
        {
            return "MEDICINE IS LOW\nSearch bathrooms or medical caches.";
        }

        bool hasInfrastructure = GetTree().GetNodesInGroup(CompletedBuilding.GroupName)
            .OfType<CompletedBuilding>()
            .Any(building => building.BuildingType is BuildingType.ProvisionsShed or BuildingType.Outpost);
        if (!hasInfrastructure)
        {
            return "IMPROVE THE SETTLEMENT\nBuild storage or an outpost.";
        }

        if (home is not null && home.SearchedContainerCount < home.ContainerCount)
        {
            return "CONTINUE SCAVENGING\nThe abandoned home still has useful supplies.";
        }

        return "EXPLORE FARTHER\nSearch the county and secure more resources.";
    }

    private bool HasRestShelter()
    {
        return GetTree().GetNodesInGroup(CompletedBuilding.GroupName)
            .OfType<CompletedBuilding>()
            .Any(building => building.ProvidesRest);
    }

    private InteriorBuildingRuntime? FindAbandonedHome()
    {
        return GetTree().GetNodesInGroup(InteriorBuildingRuntime.GroupName)
            .OfType<InteriorBuildingRuntime>()
            .FirstOrDefault(building => building.Definition.Id == SurvivalTuning.AbandonedHomeId);
    }

    private bool AnyThreatened()
    {
        Zombie[] zombies = GetTree().GetNodesInGroup(Zombie.GroupName)
            .OfType<Zombie>()
            .Where(zombie => zombie.IsAlive)
            .ToArray();
        if (zombies.Length == 0)
        {
            return false;
        }

        float radiusSquared = DangerRadius * DangerRadius;
        foreach (Survivor survivor in GetTree().GetNodesInGroup(Survivor.GroupName)
                     .OfType<Survivor>()
                     .Where(survivor => survivor.IsAlive))
        {
            if (survivor.IsSheltered())
            {
                continue;
            }

            if (zombies.Any(zombie => zombie.SimulationPosition.DistanceSquaredTo(survivor.SimulationPosition) <= radiusSquared))
            {
                return true;
            }
        }

        return false;
    }

    private void Mark(string milestoneId, string message)
    {
        if (_completedMilestones.Add(milestoneId))
        {
            Notify(message);
        }
    }

    private static void Notify(string message)
    {
        if (Current is null || Current.GetTree() is null)
        {
            return;
        }

        (Current.GetTree().GetFirstNodeInGroup(GameHud.GroupName) as GameHud)?.Notify(message);
    }
}
