#nullable enable

using System.Collections.Generic;
using System.Linq;
using AshwoodCounty.World.County.Visual;
using Godot;

namespace AshwoodCounty.World.County;

/// <summary>
/// Finite continuous county root. The macro landscape is one lightweight draw
/// surface while nearby dynamic-content chunks are represented by a small set
/// of active coordinates. Crossing a chunk or district never moves an actor.
/// </summary>
public partial class CountyWorld : Node2D
{
    [Signal] public delegate void ChunkLoadedEventHandler(Vector2I coordinate);
    [Signal] public delegate void ChunkUnloadedEventHandler(Vector2I coordinate);
    [Signal] public delegate void ActorEnteredRegionEventHandler(Node actor, string previousRegionId, string regionId);

    [Export(PropertyHint.Range, "1,4,1")]
    public int StreamingRadiusChunks { get; set; } = 2;

    [Export(PropertyHint.Range, "0.05,2.0,0.05")]
    public float StreamingUpdateInterval { get; set; } = .25f;

    [Export] public string FocusGroupName { get; set; } = "survivors";
    [Export] public bool DrawMacroLandscape { get; set; } = true;
    [Export] public bool DrawLocationLabels { get; set; }
    [Export] public bool DrawChunkDebug { get; set; }
    [Export] public bool DrawRegionDebug { get; set; }
    [Export] public bool DrawFullCountyInEditor { get; set; }

    public Rect2 CountyBounds => CountyCoordinateSpace.GridBounds;
    public Rect2 CountyGridBounds => CountyBounds;
    public Rect2 CountyCanvasBounds => CountyCoordinateSpace.ProjectedCanvasBounds();
    public Vector2 StartingCampGridPosition => CountyCoordinateSpace.StartingCamp;
    public IReadOnlyList<CountyLocationDefinition> Regions => CountyMacroLayout.Regions;
    public int LoadedChunkCount => _loadedChunks.Count;
    public IReadOnlyCollection<Vector2I> LoadedChunks => _loadedChunks;
    public IReadOnlyDictionary<Vector2I, CountyChunk> LoadedChunkNodes => _loadedChunkNodes;
    public IReadOnlyDictionary<Vector2I, CountyChunkState> ChunkStates => _chunkStates;

    private readonly HashSet<Vector2I> _loadedChunks = [];
    private readonly Dictionary<Vector2I, CountyChunk> _loadedChunkNodes = [];
    private readonly Dictionary<Vector2I, CountyChunkState> _chunkStates = [];
    private readonly Dictionary<ulong, string> _actorRegions = [];
    private readonly List<Vector2> _explicitFocusPoints = [];
    private double _streamingElapsed;
    private CountyVisualLayer? _visualLayer;

    public override void _Ready()
    {
        Visible = true;
        YSortEnabled = false;
        ZIndex = -100;
        // Opt-out is useful for renderer profiling and automated isolation; it
        // does not alter normal gameplay or editor behavior.
        bool visualsDisabled = System.Environment.GetEnvironmentVariable("ASHWOOD_DISABLE_COUNTY_VISUALS") == "1";
        if (DrawMacroLandscape && !visualsDisabled)
        {
            _visualLayer = new CountyVisualLayer
            {
                Name = "CountyVisuals",
                DrawLocationLabels = DrawLocationLabels
            };
            AddChild(_visualLayer);
            MoveChild(_visualLayer, 0);
        }
        _explicitFocusPoints.Add(StartingCampGridPosition);
        RefreshStreaming();
    }

    public override void _Process(double delta)
    {
        _streamingElapsed += delta;
        if (_streamingElapsed < StreamingUpdateInterval)
            return;

        _streamingElapsed = 0;
        RefreshStreaming();
    }

    /// <summary>Replace the fallback streaming focus used when no actors are in the focus group.</summary>
    public void SetStreamingFocus(Vector2 countyGridPosition)
    {
        _explicitFocusPoints.Clear();
        _explicitFocusPoints.Add(CountyCoordinateSpace.ClampToCounty(countyGridPosition));
        if (!Engine.IsEditorHint())
            RefreshStreaming();
    }

    public void SetStreamingFoci(IEnumerable<Vector2> countyGridPositions)
    {
        _explicitFocusPoints.Clear();
        _explicitFocusPoints.AddRange(countyGridPositions.Select(CountyCoordinateSpace.ClampToCounty));
        if (!Engine.IsEditorHint())
            RefreshStreaming();
    }

    public CountyLocationDefinition RegionAt(Vector2 countyGridPosition) =>
        CountyMacroLayout.RegionAt(countyGridPosition);

    public CountyLocationDefinition GetRegionAt(Vector2 countyGridPosition) => RegionAt(countyGridPosition);

    public CountyChunkState GetChunkState(Vector2 countyGridPosition) =>
        GetChunkState(CountyCoordinateSpace.GridToChunk(countyGridPosition));

    public CountyChunkState GetChunkState(Vector2I coordinate)
    {
        if (!_chunkStates.TryGetValue(coordinate, out CountyChunkState? state))
        {
            state = new CountyChunkState(coordinate);
            _chunkStates[coordinate] = state;
        }

        return state;
    }

    public void MarkObjectRemoved(string stableObjectId, Vector2 countyGridPosition)
    {
        if (!string.IsNullOrWhiteSpace(stableObjectId))
            GetChunkState(countyGridPosition).RemovedObjectIds.Add(stableObjectId);
    }

    public bool IsObjectRemoved(string stableObjectId, Vector2 countyGridPosition) =>
        GetChunkState(countyGridPosition).RemovedObjectIds.Contains(stableObjectId);

    public override void _Draw()
    {
        // Production landscape art is split into small CanvasItems by
        // CountyVisualLayer. Keeping this root draw list debug-only lets Godot
        // cull distant county art instead of treating the entire 384x320 map as
        // one enormous visible CanvasItem.
        if (DrawRegionDebug)
            DrawRegionBoundaries();
        if (DrawChunkDebug)
            DrawChunkBoundaries();
    }

    private void RefreshStreaming()
    {
        List<(Node Actor, Vector2 GridPosition)> actors = FindFocusActors();
        IEnumerable<Vector2> focusPoints = actors.Count > 0
            ? actors.Select(actor => actor.GridPosition)
            : _explicitFocusPoints.Count > 0 ? _explicitFocusPoints : [StartingCampGridPosition];

        HashSet<Vector2I> required = [];
        foreach (Vector2 focus in focusPoints)
            required.UnionWith(CountyCoordinateSpace.ChunksAround(focus, StreamingRadiusChunks));

        foreach (Vector2I coordinate in required.Except(_loadedChunks).ToArray())
        {
            _loadedChunks.Add(coordinate);
            CountyChunkState state = GetChunkState(coordinate);
            state.HasEverLoaded = true;
            CountyChunk chunk = new();
            chunk.Initialize(coordinate, state);
            AddChild(chunk);
            _loadedChunkNodes[coordinate] = chunk;
            EmitSignal(SignalName.ChunkLoaded, coordinate);
        }

        foreach (Vector2I coordinate in _loadedChunks.Except(required).ToArray())
        {
            _loadedChunks.Remove(coordinate);
            if (_loadedChunkNodes.Remove(coordinate, out CountyChunk? chunk))
                chunk.QueueFree();
            EmitSignal(SignalName.ChunkUnloaded, coordinate);
        }

        TrackActorRegions(actors);
        if (DrawChunkDebug)
            QueueRedraw();
    }

    private List<(Node Actor, Vector2 GridPosition)> FindFocusActors()
    {
        List<(Node Actor, Vector2 GridPosition)> result = [];
        if (string.IsNullOrWhiteSpace(FocusGroupName) || !IsInsideTree())
            return result;

        foreach (Node actor in GetTree().GetNodesInGroup(FocusGroupName))
        {
            if (actor is not Node2D actor2D || !IsInstanceValid(actor2D))
                continue;

            Vector2 localCanvasPosition = ToLocal(actor2D.GlobalPosition);
            Vector2 gridPosition = CountyCoordinateSpace.ClampToCounty(IsometricGrid.ScreenToGrid(localCanvasPosition));
            result.Add((actor, gridPosition));
        }

        return result;
    }

    private void TrackActorRegions(IEnumerable<(Node Actor, Vector2 GridPosition)> actors)
    {
        foreach ((Node actor, Vector2 position) in actors)
        {
            string regionId = RegionAt(position).Id;
            ulong actorId = actor.GetInstanceId();
            if (!_actorRegions.TryGetValue(actorId, out string? previousRegion))
            {
                _actorRegions[actorId] = regionId;
                EmitSignal(SignalName.ActorEnteredRegion, actor, string.Empty, regionId);
                continue;
            }

            if (previousRegion == regionId)
                continue;

            _actorRegions[actorId] = regionId;
            EmitSignal(SignalName.ActorEnteredRegion, actor, previousRegion, regionId);
        }
    }

    private void DrawRegionBoundaries()
    {
        foreach (CountyLocationDefinition region in CountyMacroLayout.Locations.Where(location => location.Kind == CountyLocationKind.District))
        {
            Vector2[] outline = ProjectEllipse(region.Center, region.Radius, 48);
            DrawPolyline(outline.Append(outline[0]).ToArray(), new Color(0.85f, .65f, .25f, .52f), 2f, true);
            Vector2 label = IsometricGrid.GridToScreen(region.Center);
            DrawString(ThemeDB.FallbackFont, label, region.Name, HorizontalAlignment.Center, 180, 15, new Color("#f0d99b"));
        }
    }

    private void DrawChunkBoundaries()
    {
        for (int y = 0; y < CountyCoordinateSpace.Height; y += CountyCoordinateSpace.ChunkSize)
        {
            for (int x = 0; x < CountyCoordinateSpace.Width; x += CountyCoordinateSpace.ChunkSize)
            {
                Vector2I coordinate = new(x / CountyCoordinateSpace.ChunkSize, y / CountyCoordinateSpace.ChunkSize);
                Rect2 bounds = CountyCoordinateSpace.ChunkGridBounds(coordinate);
                Vector2[] outline = IsometricGrid.ProjectRectangle(bounds.Position, bounds.Size);
                Color color = _loadedChunks.Contains(coordinate)
                    ? new Color(.95f, .67f, .18f, .58f)
                    : new Color(.05f, .08f, .05f, .24f);
                DrawPolyline(outline.Append(outline[0]).ToArray(), color, 1f, true);
            }
        }
    }

    private static Vector2[] ProjectEllipse(Vector2 center, Vector2 radius, int segments)
    {
        Vector2[] points = new Vector2[segments];
        for (int index = 0; index < segments; index++)
        {
            float angle = Mathf.Tau * index / segments;
            Vector2 gridPoint = center + new Vector2(Mathf.Cos(angle) * radius.X, Mathf.Sin(angle) * radius.Y);
            points[index] = IsometricGrid.GridToScreen(gridPoint);
        }
        return points;
    }

}
