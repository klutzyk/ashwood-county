#nullable enable

using System;
using System.Collections.Generic;
using AshwoodCounty.World;

namespace AshwoodCounty.World.Regions;

/// <summary>
/// Serializable state owned by one county region. Region scenes are disposable;
/// durable changes belong here so travelling away never resets the simulation.
/// </summary>
public sealed class RegionState
{
    public string RegionId { get; set; } = string.Empty;
    public bool Discovered { get; set; }
    public bool Reclaimed { get; set; }
    public RegionControl Control { get; set; } = RegionControl.Unknown;
    public bool HasOutpost { get; set; }
    public bool ConnectedToSettlement { get; set; }
    public int VisitCount { get; set; }
    public double LastVisitedGameMinute { get; set; }
    public Dictionary<string, bool> RemovedObjects { get; set; } = new();
    public Dictionary<string, PersistedRegionObject> Objects { get; set; } = new();
    public HashSet<string> DiscoveredLandmarks { get; set; } = new();
}

public sealed class PersistedRegionObject
{
    public string Kind { get; set; } = string.Empty;
    public float GridX { get; set; }
    public float GridY { get; set; }
    public int Amount { get; set; }
    public bool Active { get; set; } = true;
}

public sealed class CountyRegionSave
{
    public int Version { get; set; } = 1;
    public string CurrentRegionId { get; set; } = RegionIds.Outskirts;
    public Dictionary<string, RegionState> Regions { get; set; } = new();
}

public static class RegionIds
{
    public const string Outskirts = "outskirts";
    public const string FarmEdge = "farm_district";
    public const string MillCreek = "mill_creek";
}
