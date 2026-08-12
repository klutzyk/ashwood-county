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
    private readonly Dictionary<ulong, ulong> _treeClaims = [];
    private readonly Dictionary<ulong, HashSet<ulong>> _siteClaims = [];
    private double _assignmentElapsed;
    private Stockpile _stockpile = null!;

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
        if (job.Type == SettlementJobType.ChopTree && GodotObject.IsInstanceValid(job.Target))
        {
            _treeClaims.Remove(job.Target.GetInstanceId());
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
            if (TryAssignConstruction(survivor) || TryAssignTree(survivor))
            {
                continue;
            }
        }
    }

    private bool TryAssignConstruction(Survivor survivor)
    {
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

    private bool TryAssignTree(Survivor survivor)
    {
        ResolveStockpile();
        HarvestableResource tree = GetTree().GetNodesInGroup(HarvestableResource.GroupName)
            .OfType<HarvestableResource>()
            .Where(resource => resource.IsDesignatedForChop && resource.IsHarvestable
                && !_treeClaims.ContainsKey(resource.GetInstanceId()))
            .MinBy(resource => survivor.SimulationPosition.DistanceSquaredTo(resource.WorldPosition));
        if (tree is null || _stockpile is null)
        {
            return false;
        }

        ulong survivorId = survivor.GetInstanceId();
        _treeClaims[tree.GetInstanceId()] = survivorId;
        _claimsBySurvivor[survivorId] = new SettlementJob(SettlementJobType.ChopTree, tree);
        survivor.IssueAutonomousHarvestOrder(tree, _stockpile, tree.GetInteractionPosition(0, 1), _stockpile.GetInteractionPosition(0, 1));
        return true;
    }

    private void ResolveStockpile()
    {
        _stockpile ??= GetTree().GetFirstNodeInGroup(Stockpile.GroupName) as Stockpile;
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
