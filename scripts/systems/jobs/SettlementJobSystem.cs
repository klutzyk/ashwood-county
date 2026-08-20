#nullable enable annotations

using System.Collections.Generic;
using System.Linq;
using AshwoodCounty.Buildings;
using AshwoodCounty.Buildings.Interiors;
using AshwoodCounty.Items;
using AshwoodCounty.Resources;
using AshwoodCounty.Systems;
using AshwoodCounty.UI;
using AshwoodCounty.Units;
using AshwoodCounty.Units.Orders;
using Godot;

namespace AshwoodCounty.Jobs;

public partial class SettlementJobSystem : Node
{
    public const string GroupName = "settlement_job_system";
    private const double AssignmentInterval = 0.45;
    private const int MaximumAutomaticBuildersPerSite = 3;
    private const double NoTargetNotifyCooldown = 7.0;

    private readonly Dictionary<ulong, SettlementJob> _claimsBySurvivor = [];
    private readonly Dictionary<ulong, ulong> _resourceClaims = [];
    private readonly Dictionary<ulong, HashSet<ulong>> _siteClaims = [];
    private readonly Dictionary<ulong, WorkCategory> _workMandates = [];
    private readonly Dictionary<ulong, double> _noTargetCooldowns = [];
    private readonly Dictionary<ulong, string> _searchStatus = [];
    private double _assignmentElapsed;
    private Stockpile? _stockpile;
    private SettlementInventory? _inventory;
    private SettlementItemStorage? _itemStorage;

    /// <summary>Maximum county-cell distance an automatic work target may be from the survivor.</summary>
    [Export(PropertyHint.Range, "4,80,1")]
    public float WorkSearchRange { get; set; } = 30f;

    public override void _Ready()
    {
        AddToGroup(GroupName);
        Callable.From(ResolveStockpile).CallDeferred();
    }

    public override void _Process(double delta)
    {
        _assignmentElapsed += delta;
        foreach (ulong survivorId in _noTargetCooldowns.Keys.ToArray())
        {
            _noTargetCooldowns[survivorId] = Mathf.Max(0, _noTargetCooldowns[survivorId] - delta);
            if (_noTargetCooldowns[survivorId] <= 0) _noTargetCooldowns.Remove(survivorId);
        }

        if (_assignmentElapsed < AssignmentInterval)
        {
            return;
        }

        _assignmentElapsed = 0;
        CleanupClaims();
        AssignIdleSurvivors();
    }

    public void ReleaseClaim(Survivor survivor)
    {
        if (survivor is null || !_claimsBySurvivor.Remove(survivor.GetInstanceId(), out SettlementJob job))
        {
            return;
        }

        ulong survivorId = survivor.GetInstanceId();
        if (job.Type == SettlementJobType.HarvestResource && GodotObject.IsInstanceValid(job.Target))
        {
            _resourceClaims.Remove(job.Target.GetInstanceId());
        }
        else if (job.Type == SettlementJobType.Scavenge && GodotObject.IsInstanceValid(job.Target))
        {
            _resourceClaims.Remove(job.Target.GetInstanceId());
            ((ScavengeSource)job.Target).ReleaseClaim(survivorId);
        }
        else if ((job.Type is SettlementJobType.SearchContainer or SettlementJobType.EnterBuilding or SettlementJobType.Haul)
                 && GodotObject.IsInstanceValid(job.Target))
        {
            _resourceClaims.Remove(job.Target.GetInstanceId());
            if (job.Target is HaulableDrop drop) drop.ReleaseClaim(survivorId);
        }
        else if (job.Type == SettlementJobType.BuildConstruction && GodotObject.IsInstanceValid(job.Target)
            && _siteClaims.TryGetValue(job.Target.GetInstanceId(), out HashSet<ulong> builders))
        {
            builders.Remove(survivorId);
            if (builders.Count == 0)
            {
                _siteClaims.Remove(job.Target.GetInstanceId());
            }
        }
    }

    /// <summary>Target exclusively reserved by a different autonomous worker.</summary>
    public bool IsTargetClaimedByOther(ulong targetId, ulong survivorId)
    {
        return _resourceClaims.TryGetValue(targetId, out ulong claimant) && claimant != survivorId;
    }

    public GodotObject? CurrentClaimTarget(Survivor survivor)
    {
        return survivor is not null && _claimsBySurvivor.TryGetValue(survivor.GetInstanceId(), out SettlementJob job)
            ? job.Target
            : null;
    }

    public void SetWorkMandate(Survivor survivor, WorkCategory category)
    {
        if (survivor is null || !survivor.IsAlive) return;
        ulong survivorId = survivor.GetInstanceId();
        _workMandates[survivorId] = category;
        _noTargetCooldowns.Remove(survivorId);
        _searchStatus[survivorId] = CategorySearchLabel(category);
    }

    public void ClearWorkMandate(Survivor survivor)
    {
        if (survivor is null) return;
        ulong survivorId = survivor.GetInstanceId();
        _workMandates.Remove(survivorId);
        _searchStatus.Remove(survivorId);
        _noTargetCooldowns.Remove(survivorId);
        if (survivor.IsAutonomousOrder && IsMandateOrder(survivor.CurrentOrderType))
        {
            survivor.CancelCurrentOrder();
            ReleaseClaim(survivor);
        }
    }

    public void ClearWorkMandates(WorkCategory category)
    {
        foreach (Survivor survivor in GetSurvivors().Where(s => _workMandates.GetValueOrDefault(s.GetInstanceId()) == category).ToArray())
        {
            ClearWorkMandate(survivor);
        }
    }

    public bool HasWorkMandate(Survivor survivor)
    {
        return survivor is not null && _workMandates.ContainsKey(survivor.GetInstanceId());
    }

    /// <summary>Human status line shown while a mandated survivor waits for work.</summary>
    public string? WorkStatusFor(Survivor survivor)
    {
        if (survivor is null || !HasWorkMandate(survivor)) return null;
        ulong survivorId = survivor.GetInstanceId();
        return _noTargetCooldowns.TryGetValue(survivorId, out double remaining) && remaining > 0
            ? $"No {CategoryNoun(_workMandates[survivorId])} nearby"
            : _searchStatus.GetValueOrDefault(survivorId, "Searching");
    }

    /// <summary>
    /// Manual designation priority: a click on a highlighted target while the
    /// corresponding WORK mode is active assigns the survivor immediately.
    /// </summary>
    public bool PrioritizeDesignatedTarget(Survivor survivor, GodotObject target, WorkCategory category)
    {
        if (survivor is null || !survivor.IsAlive || target is null || !GodotObject.IsInstanceValid(target)) return false;
        ResolveStockpile();
        ulong survivorId = survivor.GetInstanceId();

        if ((category is WorkCategory.Woodcutting or WorkCategory.Foraging) && target is HarvestableResource resource)
        {
            ResourceType wanted = category == WorkCategory.Woodcutting ? ResourceType.Wood : ResourceType.Food;
            if (resource.ResourceType != wanted || !resource.IsHarvestable || _stockpile is null) return false;
            _resourceClaims[target.GetInstanceId()] = survivorId;
            _claimsBySurvivor[survivorId] = new SettlementJob(SettlementJobType.HarvestResource, target);
            _searchStatus.Remove(survivorId);
            survivor.CancelCurrentOrder();
            survivor.IssueAutonomousHarvestOrder(resource, _stockpile, resource.GetInteractionPosition(0, 1), _stockpile.GetInteractionPosition(0, 1));
            return true;
        }

        if (category == WorkCategory.Scavenging && target is ScavengeSource source)
        {
            if (source.IsDepleted || _stockpile is null) return false;
            source.SetScavengeDesignated(true);
            _resourceClaims[target.GetInstanceId()] = survivorId;
            _claimsBySurvivor[survivorId] = new SettlementJob(SettlementJobType.Scavenge, target);
            _searchStatus.Remove(survivorId);
            survivor.CancelCurrentOrder();
            survivor.IssueAutonomousScavengeOrder(source, _stockpile, source.GetInteractionPosition(), _stockpile.GetInteractionPosition(0, 1));
            return true;
        }

        if (category == WorkCategory.Hauling && target is HaulableDrop drop)
        {
            if (!drop.HasItems || _stockpile is null || _itemStorage is null) return false;
            _resourceClaims[target.GetInstanceId()] = survivorId;
            _claimsBySurvivor[survivorId] = new SettlementJob(SettlementJobType.Haul, target);
            _searchStatus.Remove(survivorId);
            survivor.CancelCurrentOrder();
            survivor.IssueAutonomousHaulOrder(drop, _stockpile, _itemStorage, drop.GetInteractionPosition(), _stockpile.GetInteractionPosition(0, 1));
            return true;
        }

        return false;
    }

    private void CleanupClaims()
    {
        foreach (Survivor survivor in GetSurvivors())
        {
            if (!survivor.IsAlive)
            {
                _workMandates.Remove(survivor.GetInstanceId());
                _searchStatus.Remove(survivor.GetInstanceId());
                _noTargetCooldowns.Remove(survivor.GetInstanceId());
            }

            if (_claimsBySurvivor.ContainsKey(survivor.GetInstanceId()) && !survivor.IsAutonomousOrder)
            {
                ReleaseClaim(survivor);
            }
        }
    }

    private void AssignIdleSurvivors()
    {
        bool night = SurvivalCycle.IsNightActive();
        foreach (Survivor survivor in GetSurvivors().Where(unit => unit.IsAvailableForAutonomousWork))
        {
            if (TryAssignSelfCare(survivor) || TryAssignEating(survivor))
            {
                continue;
            }

            if (HasWorkMandate(survivor))
            {
                if (TryAssignMandatedWork(survivor)) continue;
                if (survivor.Energy <= 24f && TryAssignRest(survivor)) continue;
                continue;
            }

            if (!night && (TryAssignConstruction(survivor) || TryAssignScavenging(survivor) || TryAssignResource(survivor)))
            {
                continue;
            }
        }
    }

    private bool TryAssignSelfCare(Survivor survivor)
    {
        if (survivor.Health > survivor.MaxHealth * 0.6f)
        {
            return false;
        }

        string carriedMedical = survivor.Inventory.Items
            .Where(stack => ItemCatalog.TryGet(stack.ItemId, out ItemDefinition definition)
                && definition.Category == ItemCategory.Medical && definition.Usable)
            .Select(stack => stack.ItemId)
            .FirstOrDefault();
        return carriedMedical is not null && survivor.UseItem(carriedMedical);
    }

    private bool TryAssignConstruction(Survivor survivor)
    {
        if (!survivor.AllowsWork(WorkCategory.Construction)) return false;
        ConstructionSite site = GetTree().GetNodesInGroup(ConstructionSite.GroupName)
            .OfType<ConstructionSite>()
            .Where(candidate => candidate.IsAvailableForBuilding && GetBuilderCount(candidate) < MaximumAutomaticBuildersPerSite)
            .MinBy(candidate => survivor.SimulationPosition.DistanceSquaredTo(candidate.OccupancyFootprint.Center));
        if (site is null)
        {
            return false;
        }

        ulong survivorId = survivor.GetInstanceId();
        if (!_siteClaims.TryGetValue(site.GetInstanceId(), out HashSet<ulong> builders))
        {
            builders = [];
            _siteClaims[site.GetInstanceId()] = builders;
        }

        builders.Add(survivorId);
        _claimsBySurvivor[survivorId] = new SettlementJob(SettlementJobType.BuildConstruction, site);
        survivor.IssueAutonomousBuildOrder(site, site.GetInteractionPosition(builders.Count - 1, MaximumAutomaticBuildersPerSite));
        return true;
    }

    private bool TryAssignEating(Survivor survivor)
    {
        ResolveStockpile();
        if (!survivor.NeedsMeal) return false;

        string carriedFood = survivor.Inventory.Items
            .Where(stack => ItemCatalog.TryGet(stack.ItemId, out ItemDefinition definition)
                && definition.Category == ItemCategory.Food && definition.Usable)
            .Select(stack => stack.ItemId)
            .FirstOrDefault()!;
        if (carriedFood is not null && survivor.UseItem(carriedFood)) return true;

        if (_stockpile is null || _inventory is null || !_inventory.CanAfford(ResourceType.Food, 1)) return false;
        _claimsBySurvivor[survivor.GetInstanceId()] = new SettlementJob(SettlementJobType.Eat, _stockpile);
        survivor.IssueAutonomousEatOrder(_inventory, _stockpile, _stockpile.GetInteractionPosition(0, 1));
        return true;
    }

    private bool TryAssignResource(Survivor survivor)
    {
        ResolveStockpile();
        HarvestableResource tree = GetTree().GetNodesInGroup(HarvestableResource.GroupName)
            .OfType<HarvestableResource>()
            .Where(resource => resource.IsDesignatedForHarvest && resource.IsHarvestable
                && survivor.AllowsWork(resource.ResourceType == ResourceType.Wood ? WorkCategory.Woodcutting : WorkCategory.Foraging)
                && !_resourceClaims.ContainsKey(resource.GetInstanceId()))
            .MinBy(resource => survivor.SimulationPosition.DistanceSquaredTo(resource.WorldPosition));
        if (tree is null || _stockpile is null)
        {
            return false;
        }

        ulong survivorId = survivor.GetInstanceId();
        _resourceClaims[tree.GetInstanceId()] = survivorId;
        _claimsBySurvivor[survivorId] = new SettlementJob(SettlementJobType.HarvestResource, tree);
        survivor.IssueAutonomousHarvestOrder(tree, _stockpile, tree.GetInteractionPosition(0, 1), _stockpile.GetInteractionPosition(0, 1));
        return true;
    }

    private bool TryAssignRest(Survivor survivor)
    {
        if (survivor.Energy > 28) return false;
        CompletedBuilding shelter = GetTree().GetNodesInGroup(CompletedBuilding.GroupName).OfType<CompletedBuilding>()
            .Where(building => building.ProvidesRest && building.AvailableRestSlots > 0)
            .MinBy(building => survivor.SimulationPosition.DistanceSquaredTo(building.OccupancyFootprint.Center));
        if (shelter is null) return false;
        _claimsBySurvivor[survivor.GetInstanceId()] = new SettlementJob(SettlementJobType.Rest, shelter);
        survivor.IssueAutonomousRestOrder(shelter); return true;
    }

    private bool TryAssignTreatment(Survivor healer)
    {
        ResolveStockpile();
        if (_inventory is null || !healer.AllowsWork(WorkCategory.Medical) || !_inventory.CanAfford(ResourceType.Medicine, 1)) return false;
        Survivor patient = GetSurvivors().Where(s => s != healer && s.IsAlive && s.Health < s.MaxHealth * .65f)
            .MinBy(s => healer.SimulationPosition.DistanceSquaredTo(s.SimulationPosition));
        if (patient is null) return false;
        _claimsBySurvivor[healer.GetInstanceId()] = new SettlementJob(SettlementJobType.Treat, patient);
        healer.IssueAutonomousTreatOrder(patient, _inventory); return true;
    }

    private bool TryAssignScavenging(Survivor survivor)
    {
        ResolveStockpile();
        if (_stockpile is null || !survivor.AllowsWork(WorkCategory.Scavenging)) return false;
        ScavengeSource source = GetTree().GetNodesInGroup(ScavengeSource.GroupName)
            .OfType<ScavengeSource>()
            .Where(candidate => candidate.IsDesignatedForScavenging && !candidate.IsDepleted && !candidate.IsClaimed
                && !_resourceClaims.ContainsKey(candidate.GetInstanceId()))
            .MinBy(candidate => survivor.SimulationPosition.DistanceSquaredTo(candidate.WorldPosition));
        if (source is null) return false;

        ulong survivorId = survivor.GetInstanceId();
        _resourceClaims[source.GetInstanceId()] = survivorId;
        _claimsBySurvivor[survivorId] = new SettlementJob(SettlementJobType.Scavenge, source);
        survivor.IssueAutonomousScavengeOrder(source, _stockpile, source.GetInteractionPosition(), _stockpile.GetInteractionPosition(0, 1));
        return true;
    }

    private bool TryAssignMandatedWork(Survivor survivor)
    {
        ResolveStockpile();
        if (!_workMandates.TryGetValue(survivor.GetInstanceId(), out WorkCategory category)) return false;
        return category switch
        {
            WorkCategory.Woodcutting => AssignHarvest(survivor, ResourceType.Wood),
            WorkCategory.Foraging => AssignHarvest(survivor, ResourceType.Food),
            WorkCategory.Scavenging => AssignScavengeOrInterior(survivor),
            WorkCategory.Hauling => AssignHaul(survivor),
            _ => false
        };
    }

    private bool AssignHarvest(Survivor survivor, ResourceType resourceType)
    {
        ulong survivorId = survivor.GetInstanceId();
        HarvestableResource target = GetTree().GetNodesInGroup(HarvestableResource.GroupName)
            .OfType<HarvestableResource>()
            .Where(resource => resource.ResourceType == resourceType && resource.IsHarvestable
                && InWorkRange(survivor, resource.WorldPosition)
                && !IsTargetClaimedByOther(resource.GetInstanceId(), survivorId)
                && IsExteriorTarget(survivor, resource.WorldPosition))
            .OrderBy(resource => resource.IsDesignatedForHarvest ? 0 : 1)
            .ThenBy(resource => survivor.SimulationPosition.DistanceSquaredTo(resource.WorldPosition))
            .FirstOrDefault();
        if (target is null || _stockpile is null)
        {
            return ReportNoTargets(survivor, resourceType == ResourceType.Wood ? "trees" : "forage");
        }

        _resourceClaims[target.GetInstanceId()] = survivorId;
        _claimsBySurvivor[survivorId] = new SettlementJob(SettlementJobType.HarvestResource, target);
        _searchStatus.Remove(survivorId);
        survivor.IssueAutonomousHarvestOrder(target, _stockpile, target.GetInteractionPosition(0, 1), _stockpile.GetInteractionPosition(0, 1));
        return true;
    }

    private bool AssignScavengeOrInterior(Survivor survivor)
    {
        ulong survivorId = survivor.GetInstanceId();
        ScavengeSource source = GetTree().GetNodesInGroup(ScavengeSource.GroupName)
            .OfType<ScavengeSource>()
            .Where(candidate => candidate.IsDesignatedForScavenging && !candidate.IsDepleted && !candidate.IsClaimed
                && !IsTargetClaimedByOther(candidate.GetInstanceId(), survivorId)
                && InWorkRange(survivor, candidate.WorldPosition))
            .OrderBy(candidate => survivor.SimulationPosition.DistanceSquaredTo(candidate.WorldPosition))
            .FirstOrDefault();
        if (source is not null && _stockpile is not null)
        {
            _resourceClaims[source.GetInstanceId()] = survivorId;
            _claimsBySurvivor[survivorId] = new SettlementJob(SettlementJobType.Scavenge, source);
            _searchStatus.Remove(survivorId);
            survivor.IssueAutonomousScavengeOrder(source, _stockpile, source.GetInteractionPosition(), _stockpile.GetInteractionPosition(0, 1));
            return true;
        }

        InteriorContainerRuntime container = GetTree().GetNodesInGroup(InteriorContainerRuntime.GroupName)
            .OfType<InteriorContainerRuntime>()
            .Where(candidate => !candidate.IsSearched
                && !IsTargetClaimedByOther(candidate.GetInstanceId(), survivorId)
                && InWorkRange(survivor, candidate.Building.Definition.Footprint.GetCenter())
                && (survivor.IsInsideInterior(candidate.Building) || candidate.Building.ExteriorEntrance is not null))
            .OrderBy(candidate => survivor.SimulationPosition.DistanceSquaredTo(candidate.Building.Definition.Footprint.GetCenter()))
            .FirstOrDefault();
        if (container is null)
        {
            return ReportNoTargets(survivor, "salvage");
        }

        _resourceClaims[container.GetInstanceId()] = survivorId;
        _claimsBySurvivor[survivorId] = new SettlementJob(
            survivor.IsInsideInterior(container.Building) ? SettlementJobType.SearchContainer : SettlementJobType.EnterBuilding,
            container);
        _searchStatus.Remove(survivorId);
        if (survivor.IsInsideInterior(container.Building))
        {
            survivor.IssueAutonomousSearchContainerOrder(container);
        }
        else
        {
            survivor.IssueAutonomousEnterBuildingOrder(container.Building);
        }

        return true;
    }

    private bool AssignHaul(Survivor survivor)
    {
        ulong survivorId = survivor.GetInstanceId();
        HaulableDrop drop = GetTree().GetNodesInGroup(HaulableDrop.GroupName)
            .OfType<HaulableDrop>()
            .Where(candidate => candidate.HasItems && !candidate.IsClaimed
                && !IsTargetClaimedByOther(candidate.GetInstanceId(), survivorId)
                && InWorkRange(survivor, candidate.WorldPosition)
                && IsExteriorTarget(survivor, candidate.WorldPosition))
            .OrderBy(candidate => candidate.IsDesignatedForHauling ? 0 : 1)
            .ThenBy(candidate => survivor.SimulationPosition.DistanceSquaredTo(candidate.WorldPosition))
            .FirstOrDefault();
        if (drop is null || _stockpile is null || _itemStorage is null)
        {
            return ReportNoTargets(survivor, "haulables");
        }

        _resourceClaims[drop.GetInstanceId()] = survivorId;
        _claimsBySurvivor[survivorId] = new SettlementJob(SettlementJobType.Haul, drop);
        _searchStatus.Remove(survivorId);
        survivor.IssueAutonomousHaulOrder(drop, _stockpile, _itemStorage, drop.GetInteractionPosition(), _stockpile.GetInteractionPosition(0, 1));
        return true;
    }

    private bool ReportNoTargets(Survivor survivor, string noun)
    {
        ulong survivorId = survivor.GetInstanceId();
        _searchStatus[survivorId] = $"Searching for {noun}";
        if (_noTargetCooldowns.TryGetValue(survivorId, out double remaining) && remaining > 0) return false;
        _noTargetCooldowns[survivorId] = NoTargetNotifyCooldown;
        NotifyHud($"{survivor.Profile.DisplayName}\nNo {noun} nearby");
        return false;
    }

    private bool InWorkRange(Survivor survivor, Vector2 position)
    {
        return survivor.SimulationPosition.DistanceTo(position) <= WorkSearchRange;
    }

    private static bool IsExteriorTarget(Survivor survivor, Vector2 position)
    {
        if (survivor.CurrentInterior is not null)
        {
            return survivor.CurrentInterior.Definition.Footprint.Grow(-0.10f).HasPoint(position);
        }

        foreach (Node node in survivor.GetTree().GetNodesInGroup(InteriorBuildingRuntime.GroupName))
        {
            if (node is InteriorBuildingRuntime building && building.Definition.Footprint.Grow(-0.10f).HasPoint(position))
            {
                return false;
            }
        }

        return true;
    }

    private void ResolveStockpile()
    {
        _stockpile ??= GetTree().GetFirstNodeInGroup(Stockpile.GroupName) as Stockpile;
        _inventory ??= GetTree().GetFirstNodeInGroup(SettlementInventory.GroupName) as SettlementInventory;
        _itemStorage ??= GetTree().GetFirstNodeInGroup(SettlementItemStorage.GroupName) as SettlementItemStorage;
    }

    private int GetBuilderCount(ConstructionSite site)
    {
        return _siteClaims.TryGetValue(site.GetInstanceId(), out HashSet<ulong> builders) ? builders.Count : 0;
    }

    private IEnumerable<Survivor> GetSurvivors()
    {
        return GetTree().GetNodesInGroup(Survivor.GroupName).OfType<Survivor>();
    }

    private void NotifyHud(string message)
    {
        if (GetTree().GetFirstNodeInGroup(GameHud.GroupName) is GameHud hud) hud.Notify(message);
    }

    private static bool IsMandateOrder(SurvivorOrderType type)
    {
        return type is SurvivorOrderType.HarvestResource or SurvivorOrderType.Scavenge
            or SurvivorOrderType.SearchContainer or SurvivorOrderType.EnterBuilding or SurvivorOrderType.Haul;
    }

    private static string CategorySearchLabel(WorkCategory category) => $"Searching for {CategoryNoun(category)}";

    private static string CategoryNoun(WorkCategory category) => category switch
    {
        WorkCategory.Woodcutting => "trees",
        WorkCategory.Foraging => "forage",
        WorkCategory.Scavenging => "salvage",
        WorkCategory.Hauling => "haulables",
        _ => "work"
    };
}
