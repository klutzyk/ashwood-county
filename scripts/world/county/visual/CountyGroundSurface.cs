#nullable enable

using System.Linq;
using Godot;

namespace AshwoodCounty.World.County.Visual;

/// <summary>
/// One shared procedural terrain texture, drawn through small cullable county
/// chunks. The bitmap is generated once and retained by TextureRegistry.
/// </summary>
public partial class CountyGroundSurface : Node2D
{
    private const string GroundTexturePath = "res://assets/art/terrain/county_ground.png";

    public override void _Ready()
    {
        ZAsRelative = false;
        ZIndex = -120;

        Texture2D countyTexture = TextureRegistry.Get(GroundTexturePath);
        int columns = Mathf.CeilToInt(CountyCoordinateSpace.Width / (float)CountyCoordinateSpace.ChunkSize);
        int rows = Mathf.CeilToInt(CountyCoordinateSpace.Height / (float)CountyCoordinateSpace.ChunkSize);
        for (int y = 0; y < rows; y++)
        {
            for (int x = 0; x < columns; x++)
            {
                CountyGroundChunk chunk = new() { Name = $"Ground_{x}_{y}" };
                chunk.Initialize(new Vector2I(x, y), countyTexture);
                AddChild(chunk);
            }
        }
    }

}

internal partial class CountyGroundChunk : Node2D
{
    private Rect2 _gridBounds;
    private Vector2 _canvasOrigin;
    private Texture2D? _texture;

    public void Initialize(Vector2I coordinate, Texture2D texture)
    {
        _gridBounds = CountyCoordinateSpace.ChunkGridBounds(coordinate);
        _canvasOrigin = IsometricGrid.GridToScreen(_gridBounds.Position);
        _texture = texture;
        Position = _canvasOrigin;
        ZAsRelative = false;
        ZIndex = -120;
    }

    public override void _Ready() => QueueRedraw();

    public override void _Draw()
    {
        if (_texture is null) return;
        Vector2[] points = IsometricGrid.ProjectRectangle(_gridBounds.Position, _gridBounds.Size)
            .Select(point => point - _canvasOrigin).ToArray();
        Vector2 boundsSize = CountyCoordinateSpace.GridBounds.Size;
        Vector2[] uv =
        [
            _gridBounds.Position / boundsSize,
            new Vector2(_gridBounds.End.X, _gridBounds.Position.Y) / boundsSize,
            _gridBounds.End / boundsSize,
            new Vector2(_gridBounds.Position.X, _gridBounds.End.Y) / boundsSize
        ];
        Color[] colors = [Colors.White, Colors.White, Colors.White, Colors.White];
        DrawPolygon(points, colors, uv, _texture);
    }

    public override void _ExitTree()
    {
        // Draw commands store the texture RID, but a local managed reference
        // is only needed while this chunk is alive. TextureRegistry remains
        // the single long-lived owner.
        _texture = null;
    }
}
