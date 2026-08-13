using System.Collections.Generic;

namespace AshwoodCounty.World.Fog;

/// <summary>
/// Serialization-friendly fog state. ExploredCells contains row-major indices
/// relative to Origin. Current visibility is deliberately recalculated.
/// </summary>
public sealed class CountyFogSnapshot
{
    public int Version { get; set; } = 1;
    public int OriginX { get; set; }
    public int OriginY { get; set; }
    public int Width { get; set; }
    public int Height { get; set; }
    public List<int> ExploredCells { get; set; } = new();
    public List<FogRevealPointState> Outposts { get; set; } = new();
}

/// <summary>A persistent reveal source owned by a completed outpost.</summary>
public sealed class FogRevealPointState
{
    public string Id { get; set; } = string.Empty;
    public float GridX { get; set; }
    public float GridY { get; set; }
    public float Radius { get; set; }
}
