#nullable enable

using System;
using System.Collections.Generic;
using Godot;
using System.Linq;
using AshwoodCounty.Buildings;
using AshwoodCounty.Threats;

namespace AshwoodCounty.World.Regions;

/// <summary>
/// Swaps only disposable region content. Add global survivors, inventory, clock,
/// camera and HUD beside this node, never below its RegionContent child.
/// </summary>
public partial class RegionManager : Node
{
    [Signal] public delegate void RegionChangingEventHandler(string fromRegionId, string toRegionId);
    [Signal] public delegate void RegionChangedEventHandler(string regionId, Vector2 arrivalCell);
    [Signal] public delegate void RegionTravelFailedEventHandler(string regionId, string reason);

    [Export] public NodePath ContentHostPath { get; set; } = "RegionContent";
    [Export] public string StartingRegionId { get; set; } = RegionIds.Outskirts;
    [Export] public bool LoadPersistedStateOnReady { get; set; }

    public RegionStateStore StateStore { get; } = new();
    public RegionEnvironment? CurrentEnvironment { get; private set; }
    public string CurrentRegionId => StateStore.CurrentRegionId;

    private Node? _contentHost;
    private readonly Dictionary<string, string> _scenePaths = new()
    {
        [RegionIds.Outskirts] = "res://scenes/regions/OutskirtsRegion.tscn",
        [RegionIds.FarmEdge] = "res://scenes/regions/FarmEdgeRegion.tscn",
        [RegionIds.MillCreek] = "res://scenes/regions/MillCreekRegion.tscn"
    };
    private double _controlCheck;

    public override void _Ready()
    {
        _contentHost = GetNodeOrNull<Node>(ContentHostPath);
        if (_contentHost is null)
        {
            _contentHost = new Node2D { Name = "RegionContent" };
            AddChild(_contentHost);
        }

        bool loaded = LoadPersistedStateOnReady && StateStore.Load();
        if (!loaded)
            StateStore.CurrentRegionId = StartingRegionId;
        CallDeferred(MethodName.LoadInitialRegion);
    }

    public bool CanTravelTo(string regionId)
    {
        if (!_scenePaths.ContainsKey(regionId)) return false;
        if (CurrentEnvironment is null && regionId == CurrentRegionId) return true;
        RegionDefinition current = RegionCatalog.Find(CurrentRegionId);
        return current is not null && current.Neighbors.Contains(regionId);
    }

    public bool TravelTo(string regionId, double gameMinute = 0)
    {
        if (_contentHost is null || !CanTravelTo(regionId) || !_scenePaths.TryGetValue(regionId, out string? scenePath))
        {
            EmitSignal(SignalName.RegionTravelFailed, regionId, "Region is not playable yet.");
            return false;
        }

        PackedScene? packedScene = ResourceLoader.Load<PackedScene>(scenePath);
        RegionEnvironment? next = packedScene?.Instantiate<RegionEnvironment>();
        if (next is null)
        {
            EmitSignal(SignalName.RegionTravelFailed, regionId, "Region scene could not be loaded.");
            return false;
        }

        string previousId = CurrentEnvironment?.RegionId ?? StateStore.CurrentRegionId;
        EmitSignal(SignalName.RegionChanging, previousId, regionId);
        if (CurrentEnvironment is not null)
        {
            RegionState previous = CurrentEnvironment.CaptureState();
            previous.LastVisitedGameMinute = gameMinute;
            CurrentEnvironment.QueueFree();
        }

        RegionState state = StateStore.GetOrCreate(regionId);
        state.Discovered = true;
        if (state.Control == RegionControl.Unknown) state.Control = RegionControl.Contested;
        state.VisitCount++;
        state.LastVisitedGameMinute = gameMinute;
        StateStore.CurrentRegionId = regionId;
        CurrentEnvironment = next;
        _contentHost.AddChild(next);
        next.RestoreState(state);
        EmitSignal(SignalName.RegionChanged, regionId, next.ArrivalCell);
        return true;
    }

    public bool SaveCountyState(string path = "user://county_regions.json") => StateStore.Save(path);

    private void LoadInitialRegion() => TravelTo(StateStore.CurrentRegionId);

    public override void _Process(double delta)
    {
        _controlCheck -= delta; if (_controlCheck > 0 || CurrentEnvironment is null) return; _controlCheck = 1;
        RegionState state=StateStore.GetOrCreate(CurrentRegionId);
        bool outpost=GetTree().GetNodesInGroup(CompletedBuilding.GroupName).OfType<CompletedBuilding>().Any(b=>b.BuildingType==BuildingType.Outpost&&b.RegionId==CurrentRegionId);
        bool threats=GetTree().GetNodesInGroup(Zombie.GroupName).OfType<Zombie>().Any(z=>z.IsAlive);
        state.HasOutpost=outpost;
        if(!threats&&state.Control==RegionControl.Contested)state.Control=RegionControl.Secured;
        if(outpost&&!threats){state.Control=RegionControl.Settled;state.Reclaimed=true;}
        state.ConnectedToSettlement=CurrentRegionId==RegionIds.Outskirts||state.HasOutpost;
    }
}
