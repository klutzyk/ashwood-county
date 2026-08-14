using System.Collections.Generic;
using AshwoodCounty.Buildings.Interiors;
using Godot;

namespace AshwoodCounty.World.County;

/// <summary>
/// Durable state boundary for one chunk. Runtime content generators should use
/// stable object IDs and consult this object when a chunk streams back in.
/// </summary>
public sealed class CountyChunkState(Vector2I coordinate)
{
    public Vector2I Coordinate { get; } = coordinate;
    public HashSet<string> RemovedObjectIds { get; } = [];
    public Dictionary<string, CountyObjectSnapshot> Objects { get; } = [];
    public Dictionary<string, InteriorBuildingRuntimeState> Buildings { get; } = [];
    public HashSet<string> DiscoveredLandmarkIds { get; } = [];
    public bool HasEverLoaded { get; internal set; }
}

public sealed class CountyObjectSnapshot
{
    public string Kind { get; set; } = string.Empty;
    public Vector2 GridPosition { get; set; }
    public int Amount { get; set; }
    public bool Active { get; set; } = true;
}
