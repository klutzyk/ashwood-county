#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using AshwoodCounty.Units;
using Godot;

namespace AshwoodCounty.World.Fog;

/// <summary>
/// Finite county-space fog of war. Survivors reveal nearby cells, completed
/// outposts can register persistent reveal sources, and explored knowledge can
/// be captured/restored without retaining scene nodes.
/// </summary>
public sealed partial class CountyFogOfWar : Node2D
{
    [Signal]
    public delegate void FogChangedEventHandler(int exploredCellCount, int visibleCellCount);

    [ExportGroup("County Bounds")]
    [Export] public Vector2I CountyOrigin { get; set; } = Vector2I.Zero;
    [Export] public Vector2I CountySize { get; set; } = new(160, 120);
    [Export(PropertyHint.Range, "4,32,1")] public int ChunkSize { get; set; } = 12;

    [ExportGroup("Reveal")]
    [Export(PropertyHint.Range, "1,24,0.5")] public float SurvivorRevealRadius { get; set; } = 7f;
    [Export(PropertyHint.Range, "1,32,0.5")] public float DefaultOutpostRevealRadius { get; set; } = 10f;
    [Export(PropertyHint.Range, "0.05,2,0.05")] public float UpdateIntervalSeconds { get; set; } = 0.2f;
    [Export] public bool SurvivorsRevealFog { get; set; } = true;
    [Export] public string SurvivorGroup { get; set; } = Survivor.GroupName;

    [ExportGroup("Appearance")]
    [Export] public Color UnexploredColor { get; set; } = new("11160fba");
    [Export] public Color ExploredColor { get; set; } = new("26302745");
    [Export(PropertyHint.Range, "1,3,1")] public int EdgeFeatherCells { get; set; } = 2;
    [Export] public FogDebugMode DebugMode { get; set; }
    [Export] public Color DebugUnexploredColor { get; set; } = new("8f2438b8");
    [Export] public Color DebugExploredColor { get; set; } = new("c08a32a0");
    [Export] public Color DebugVisibleColor { get; set; } = new("4f9e62a0");

    public int ExploredCellCount => _explored.Count;
    public int VisibleCellCount => _visible.Count;
    public float ExploredRatio
        => CountySize.X <= 0 || CountySize.Y <= 0
            ? 0f
            : (float)_explored.Count / (CountySize.X * CountySize.Y);

    private readonly HashSet<Vector2I> _explored = new();
    private HashSet<Vector2I> _visible = new();
    private readonly Dictionary<Vector2I, CountyFogChunk> _chunks = new();
    private readonly Dictionary<string, RevealPoint> _outposts = new(StringComparer.Ordinal);
    private double _untilUpdate;
    private FogDebugMode _lastDebugMode;

    public override void _Ready()
    {
        Visible = true;
        ZIndex = 3000;
        BuildChunks();
        _lastDebugMode = DebugMode;
        RefreshVisibility();
    }

    public override void _Process(double delta)
    {
        if (_lastDebugMode != DebugMode)
        {
            _lastDebugMode = DebugMode;
            RedrawAllChunks();
        }

        _untilUpdate -= delta;
        if (_untilUpdate > 0)
            return;

        _untilUpdate = Math.Max(0.05, UpdateIntervalSeconds);
        RefreshVisibility();
    }

    /// <summary>Returns whether the cell lies inside the configured finite county.</summary>
    public bool ContainsCell(Vector2I cell)
    {
        return cell.X >= CountyOrigin.X && cell.Y >= CountyOrigin.Y
            && cell.X < CountyOrigin.X + CountySize.X
            && cell.Y < CountyOrigin.Y + CountySize.Y;
    }

    public FogCellVisibility GetCellVisibility(Vector2I cell)
    {
        if (_visible.Contains(cell)) return FogCellVisibility.Visible;
        return _explored.Contains(cell) ? FogCellVisibility.Explored : FogCellVisibility.Unexplored;
    }

    public bool IsExplored(Vector2I cell) => _explored.Contains(cell);
    public bool IsCurrentlyVisible(Vector2I cell) => _visible.Contains(cell);
    public bool IsExplored(Vector2 countyGridPosition)
        => IsExplored(ToCell(countyGridPosition));
    public bool IsCurrentlyVisible(Vector2 countyGridPosition)
        => IsCurrentlyVisible(ToCell(countyGridPosition));
    public FogCellVisibility GetVisibilityAt(Vector2 countyGridPosition)
        => GetCellVisibility(ToCell(countyGridPosition));

    /// <summary>
    /// Registers or updates an outpost reveal source. Use the outpost's stable
    /// persistence id, not its transient Godot instance id.
    /// </summary>
    public void RegisterOutpostReveal(string outpostId, Vector2 countyGridPosition, float radius = -1f)
    {
        if (string.IsNullOrWhiteSpace(outpostId))
            throw new ArgumentException("An outpost reveal source requires a stable id.", nameof(outpostId));

        _outposts[outpostId] = new RevealPoint(
            countyGridPosition,
            radius > 0 ? radius : DefaultOutpostRevealRadius);
        RefreshVisibility();
    }

    public bool RemoveOutpostReveal(string outpostId)
    {
        bool removed = _outposts.Remove(outpostId);
        if (removed) RefreshVisibility();
        return removed;
    }

    /// <summary>Immediately explores an area without keeping it currently visible.</summary>
    public void ExploreArea(Vector2 countyGridPosition, float radius)
    {
        HashSet<Vector2I> revealed = new();
        AddRevealCircle(revealed, countyGridPosition, radius);
        HashSet<Vector2I> changed = new(revealed.Where(_explored.Add));
        RedrawChangedChunks(changed);
        if (changed.Count > 0)
            EmitSignal(SignalName.FogChanged, _explored.Count, _visible.Count);
    }

    /// <summary>Rebuilds current visibility from survivors and registered outposts.</summary>
    public void RefreshVisibility()
    {
        if (!IsInsideTree())
            return;

        HashSet<Vector2I> nextVisible = new();
        if (SurvivorsRevealFog)
        {
            foreach (Node node in GetTree().GetNodesInGroup(SurvivorGroup))
            {
                if (node is Survivor { IsAlive: true } survivor)
                    AddRevealCircle(nextVisible, survivor.SimulationPosition, SurvivorRevealRadius);
            }
        }

        foreach (RevealPoint outpost in _outposts.Values)
            AddRevealCircle(nextVisible, outpost.Position, outpost.Radius);

        HashSet<Vector2I> changed = new(_visible);
        changed.SymmetricExceptWith(nextVisible);
        _visible = nextVisible;
        foreach (Vector2I cell in _visible)
        {
            if (_explored.Add(cell))
                changed.Add(cell);
        }

        RedrawChangedChunks(changed);
        if (changed.Count > 0)
            EmitSignal(SignalName.FogChanged, _explored.Count, _visible.Count);
    }

    public CountyFogSnapshot CaptureState()
    {
        CountyFogSnapshot snapshot = new()
        {
            OriginX = CountyOrigin.X,
            OriginY = CountyOrigin.Y,
            Width = CountySize.X,
            Height = CountySize.Y
        };

        foreach (Vector2I cell in _explored.OrderBy(cell => cell.Y).ThenBy(cell => cell.X))
            snapshot.ExploredCells.Add(ToCellIndex(cell));

        foreach ((string id, RevealPoint point) in _outposts.OrderBy(pair => pair.Key))
        {
            snapshot.Outposts.Add(new FogRevealPointState
            {
                Id = id,
                GridX = point.Position.X,
                GridY = point.Position.Y,
                Radius = point.Radius
            });
        }

        return snapshot;
    }

    public void RestoreState(CountyFogSnapshot? snapshot)
    {
        _explored.Clear();
        _visible.Clear();
        _outposts.Clear();

        if (snapshot is not null && snapshot.Version == 1)
        {
            foreach (int index in snapshot.ExploredCells)
            {
                Vector2I cell = FromSnapshotCellIndex(snapshot, index);
                if (ContainsCell(cell)) _explored.Add(cell);
            }

            foreach (FogRevealPointState outpost in snapshot.Outposts)
            {
                if (!string.IsNullOrWhiteSpace(outpost.Id) && outpost.Radius > 0)
                    _outposts[outpost.Id] = new RevealPoint(new Vector2(outpost.GridX, outpost.GridY), outpost.Radius);
            }
        }

        RedrawAllChunks();
        RefreshVisibility();
    }

    public void ClearExploration()
    {
        _explored.Clear();
        _visible.Clear();
        RedrawAllChunks();
        RefreshVisibility();
    }

    internal Color GetDrawColor(FogCellVisibility state, FogDebugMode debugMode)
    {
        if (debugMode == FogDebugMode.StateColors)
        {
            return state switch
            {
                FogCellVisibility.Unexplored => DebugUnexploredColor,
                FogCellVisibility.Explored => DebugExploredColor,
                _ => DebugVisibleColor
            };
        }

        return state switch
        {
            FogCellVisibility.Unexplored => UnexploredColor,
            FogCellVisibility.Explored => ExploredColor,
            _ => Colors.Transparent
        };
    }

    /// <summary>
    /// Returns the blended fog color at a grid vertex. Drawing these shared
    /// samples as per-vertex colors makes visibility boundaries soft without
    /// adding translucent overlay nodes, shaders, or a county-sized texture.
    /// </summary>
    internal Color GetFeatheredDrawColor(Vector2I vertex)
    {
        int feather = Math.Clamp(EdgeFeatherCells, 1, 3);
        float totalWeight = 0f;
        float accumulatedAlpha = 0f;
        float premultipliedRed = 0f;
        float premultipliedGreen = 0f;
        float premultipliedBlue = 0f;

        // Sample cell centers around this shared vertex. A compact radial bell
        // gives a two-cell transition by default while keeping redraw work local.
        for (int y = vertex.Y - feather; y < vertex.Y + feather; y++)
        {
            for (int x = vertex.X - feather; x < vertex.X + feather; x++)
            {
                Vector2I cell = new(x, y);
                Color sample = GetDrawColor(
                    ContainsCell(cell) ? GetCellVisibility(cell) : FogCellVisibility.Unexplored,
                    FogDebugMode.Disabled);
                float dx = x + 0.5f - vertex.X;
                float dy = y + 0.5f - vertex.Y;
                float distanceSquared = dx * dx + dy * dy;
                float weight = 1f / (0.45f + distanceSquared);
                float weightedAlpha = sample.A * weight;

                totalWeight += weight;
                accumulatedAlpha += weightedAlpha;
                premultipliedRed += sample.R * weightedAlpha;
                premultipliedGreen += sample.G * weightedAlpha;
                premultipliedBlue += sample.B * weightedAlpha;
            }
        }

        if (totalWeight <= 0.001f)
            return Colors.Transparent;

        float alpha = accumulatedAlpha / totalWeight;
        if (alpha <= 0.001f || accumulatedAlpha <= 0.001f)
            return Colors.Transparent;

        return new Color(
            premultipliedRed / accumulatedAlpha,
            premultipliedGreen / accumulatedAlpha,
            premultipliedBlue / accumulatedAlpha,
            alpha);
    }

    private void BuildChunks()
    {
        foreach (CountyFogChunk chunk in _chunks.Values)
            chunk.QueueFree();
        _chunks.Clear();

        int safeChunkSize = Math.Max(4, ChunkSize);
        int right = CountyOrigin.X + Math.Max(1, CountySize.X);
        int bottom = CountyOrigin.Y + Math.Max(1, CountySize.Y);
        for (int y = CountyOrigin.Y; y < bottom; y += safeChunkSize)
        {
            for (int x = CountyOrigin.X; x < right; x += safeChunkSize)
            {
                Vector2I chunkCoordinate = new(
                    FloorDiv(x - CountyOrigin.X, safeChunkSize),
                    FloorDiv(y - CountyOrigin.Y, safeChunkSize));
                CountyFogChunk chunk = new() { Name = $"FogChunk_{chunkCoordinate.X}_{chunkCoordinate.Y}" };
                chunk.Configure(this, new Vector2I(x, y), new Vector2I(
                    Math.Min(safeChunkSize, right - x),
                    Math.Min(safeChunkSize, bottom - y)));
                chunk.LightMask = 0;
                _chunks[chunkCoordinate] = chunk;
                AddChild(chunk);
            }
        }
    }

    private void AddRevealCircle(HashSet<Vector2I> destination, Vector2 center, float radius)
    {
        float safeRadius = Math.Max(0, radius);
        float radiusSquared = safeRadius * safeRadius;
        int minX = Mathf.FloorToInt(center.X - safeRadius);
        int maxX = Mathf.CeilToInt(center.X + safeRadius);
        int minY = Mathf.FloorToInt(center.Y - safeRadius);
        int maxY = Mathf.CeilToInt(center.Y + safeRadius);
        for (int y = minY; y <= maxY; y++)
        {
            for (int x = minX; x <= maxX; x++)
            {
                Vector2I cell = new(x, y);
                if (!ContainsCell(cell)) continue;
                Vector2 cellCenter = new(x + 0.5f, y + 0.5f);
                if (cellCenter.DistanceSquaredTo(center) <= radiusSquared)
                    destination.Add(cell);
            }
        }
    }

    private void RedrawChangedChunks(IEnumerable<Vector2I> changedCells)
    {
        HashSet<Vector2I> dirtyChunks = new();
        int safeChunkSize = Math.Max(4, ChunkSize);
        int feather = Math.Clamp(EdgeFeatherCells, 1, 3);
        foreach (Vector2I cell in changedCells)
        {
            if (!ContainsCell(cell)) continue;
            int minChunkX = FloorDiv(cell.X - feather - CountyOrigin.X, safeChunkSize);
            int maxChunkX = FloorDiv(cell.X + feather - CountyOrigin.X, safeChunkSize);
            int minChunkY = FloorDiv(cell.Y - feather - CountyOrigin.Y, safeChunkSize);
            int maxChunkY = FloorDiv(cell.Y + feather - CountyOrigin.Y, safeChunkSize);
            for (int chunkY = minChunkY; chunkY <= maxChunkY; chunkY++)
            {
                for (int chunkX = minChunkX; chunkX <= maxChunkX; chunkX++)
                    dirtyChunks.Add(new Vector2I(chunkX, chunkY));
            }
        }

        foreach (Vector2I coordinate in dirtyChunks)
        {
            if (_chunks.TryGetValue(coordinate, out CountyFogChunk? chunk))
                chunk.QueueRedraw();
        }
    }

    private void RedrawAllChunks()
    {
        foreach (CountyFogChunk chunk in _chunks.Values)
            chunk.QueueRedraw();
    }

    private int ToCellIndex(Vector2I cell)
        => (cell.Y - CountyOrigin.Y) * CountySize.X + cell.X - CountyOrigin.X;

    private static Vector2I ToCell(Vector2 countyGridPosition)
        => new(Mathf.FloorToInt(countyGridPosition.X), Mathf.FloorToInt(countyGridPosition.Y));

    private static Vector2I FromSnapshotCellIndex(CountyFogSnapshot snapshot, int index)
    {
        int width = Math.Max(1, snapshot.Width);
        return new Vector2I(snapshot.OriginX + index % width, snapshot.OriginY + index / width);
    }

    private static int FloorDiv(int value, int divisor)
    {
        int quotient = value / divisor;
        int remainder = value % divisor;
        return remainder < 0 ? quotient - 1 : quotient;
    }

    private readonly record struct RevealPoint(Vector2 Position, float Radius);
}
