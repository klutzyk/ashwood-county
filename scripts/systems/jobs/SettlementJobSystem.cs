using System.Collections.Generic;
using System.Linq;
using AshwoodCounty.Buildings;
using AshwoodCounty.Resources;
using AshwoodCounty.Units;
using Godot;

namespace AshwoodCounty.Jobs;

public partial class SettlementJobSystem : Node
{
    public const string GroupName = "settlement_job_system";
    private const double AssignmentInterval = 0.45;
    private const int MaximumAutomaticBuildersPerSite = 3;

    private readonly Dictionary<ulong, SettlementJob> _claimsBySurvivor = [];
    private readonly Dictionary<ulong, ulong> _resourceClaims = [];
    private readonly Dictionary<ulong, HashSet<ulong>> _siteClaims = [];
    private double _assignmentElapsed;
    private Stockpile _stockpile = null!;
    private SettlementInventory _inventory = null!;

    public override void _Ready()
    {
        AddToGroup(GroupName);
        Callable.From(ResolveStockpile).CallDeferred();
    }

    public override void _Process(double delta)
    {
        _assignmentElapsed += delta;
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

    private void CleanupClaims()
    {
        foreach (Survivor survivor in GetSurvivors())
        {
            if (_claimsBySurvivor.ContainsKey(survivor.GetInstanceId()) && !survivor.IsAutonomousOrder)
            {
                ReleaseClaim(survivor);
            }
        }
    }

    private void AssignIdleSurvivors()
    {
        foreach (Survivor survivor in GetSurvivors().Where(unit => unit.IsAvailableForAutonomousWork))
        {
            if (TryAssignEating(survivor) || TryAssignRest(survivor) || TryAssignTreatment(survivor) || TryAssignConstruction(survivor) || TryAssignScavenging(survivor) || TryAssignResource(survivor))
            {
                continue;
            }
        }
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
        if (!survivor.NeedsMeal || _stockpile is null || _inventory is null
            || !_inventory.CanAfford(ResourceType.Food, 1)) return false;
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

    private void ResolveStockpile()
    {
        _stockpile ??= GetTree().GetFirstNodeInGroup(Stockpile.GroupName) as Stockpile;
        _inventory ??= GetTree().GetFirstNodeInGroup(SettlementInventory.GroupName) as SettlementInventory;
    }

    private int GetBuilderCount(ConstructionSite site)
    {
        return _siteClaims.TryGetValue(site.GetInstanceId(), out HashSet<ulong> builders) ? builders.Count : 0;
    }

    private IEnumerable<Survivor> GetSurvivors()
    {
        return GetTree().GetNodesInGroup(Survivor.GroupName).OfType<Survivor>();
    }
}
