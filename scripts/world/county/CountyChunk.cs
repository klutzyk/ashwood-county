using Godot;

namespace AshwoodCounty.World.County;

/// <summary>
/// Lightweight runtime content host for a nearby county cell. It deliberately
/// draws nothing: the macro surface hides boundaries, while generators may add
/// vegetation, resources, threats, and structures below this node.
/// </summary>
public partial class CountyChunk : Node2D
{
    public Vector2I Coordinate { get; private set; }
    public Rect2 GridBounds { get; private set; }
    public CountyChunkState State { get; private set; } = null!;

    public void Initialize(Vector2I coordinate, CountyChunkState state)
    {
        Coordinate = coordinate;
        GridBounds = CountyCoordinateSpace.ChunkGridBounds(coordinate);
        State = state;
        Name = $"Chunk_{coordinate.X}_{coordinate.Y}";
        Position = IsometricGrid.GridToScreen(GridBounds.Position);
        YSortEnabled = true;
        ZAsRelative = false;
        ZIndex = 0;
    }

    public Vector2 CountyGridToLocalCanvas(Vector2 countyGridPosition) =>
        IsometricGrid.GridToScreen(countyGridPosition) - Position;

    public Vector2 LocalCanvasToCountyGrid(Vector2 localCanvasPosition) =>
        IsometricGrid.ScreenToGrid(localCanvasPosition + Position);
}
