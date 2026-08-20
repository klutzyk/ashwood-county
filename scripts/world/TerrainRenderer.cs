using Godot;

namespace AshwoodCounty.World;

/// <summary>
/// The starting area's floor used to be a single flat blanket drawn here. That
/// ground, its roads and its dressing are now painted by the county terrain
/// layers from authored isometric art, so at runtime this node only owns the
/// optional debug grid. The editor still gets a cheap blockout preview.
/// </summary>
[Tool]
public partial class TerrainRenderer : Node2D
{
    private static readonly Color Grass = new("#4d6a3f");
    private static readonly Color GrassEdge = new("#567b3d");
    private int _width = IsometricWorld.MapWidth;
    private int _height = IsometricWorld.MapHeight;
    private bool _runtimeGridVisible;

    private readonly (Vector2 Position, Vector2 Radius, Color Color)[] _broadVariations =
    [
        (new(6, 7), new(9, 6), new Color(.10f, .19f, .05f, .21f)),
        (new(35, 7), new(9, 6), new Color(.13f, .22f, .06f, .18f)),
        (new(7, 31), new(10, 6), new Color(.12f, .20f, .06f, .22f)),
        (new(36, 31), new(9, 6), new Color(.10f, .18f, .05f, .23f)),
        (new(21, 20), new(11, 8), new Color(.42f, .35f, .16f, .12f)),
        (new(17, 16), new(7, 5), new Color(.55f, .48f, .24f, .08f)),
        (new(29, 25), new(8, 5), new Color(.26f, .31f, .10f, .12f))
    ];

    public bool IsGridVisible => Engine.IsEditorHint() || _runtimeGridVisible;

    public void Configure(int width, int height)
    {
        _width = width;
        _height = height;
        QueueRedraw();
    }

    public override void _Draw()
    {
        if (Engine.IsEditorHint())
        {
            DrawLightweightEditorPreview();
            return;
        }

        if (!_runtimeGridVisible)
        {
            return;
        }

        for (int y = 0; y < _height; y++)
        {
            for (int x = 0; x < _width; x++)
            {
                Vector2I cell = new(x, y);
                Vector2[] diamond = IsometricGrid.CellDiamond(cell);
                DrawPolyline([diamond[0], diamond[1], diamond[2], diamond[3], diamond[0]], GrassEdge, 1.0f, true);
            }
        }
    }

    private void DrawLightweightEditorPreview()
    {
        // Never load/draw a county-sized composite from a Tool CanvasItem.
        // Godot 4.7.1's 2D editor caches that custom texture draw at enormous
        // memory cost. This uses the same projection and preserves a useful
        // terrain/grid preview with a tiny fixed command count.
        Vector2[] terrain = IsometricGrid.ProjectRectangle(Vector2.Zero, new Vector2(_width, _height));
        DrawColoredPolygon(terrain, Grass);
        DrawBroadVariations();
        Color grid = new(GrassEdge, .55f);
        for (int x = 0; x <= _width; x++)
            DrawLine(IsometricGrid.GridToScreen(new Vector2(x, 0)), IsometricGrid.GridToScreen(new Vector2(x, _height)), grid, 1, true);
        for (int y = 0; y <= _height; y++)
            DrawLine(IsometricGrid.GridToScreen(new Vector2(0, y)), IsometricGrid.GridToScreen(new Vector2(_width, y)), grid, 1, true);
    }

    private void DrawBroadVariations()
    {
        foreach ((Vector2 gridPosition, Vector2 radius, Color color) in _broadVariations)
        {
            Vector2 center = IsometricGrid.GridToScreen(gridPosition);
            Vector2[] points = new Vector2[48];
            for (int index = 0; index < points.Length; index++)
            {
                float angle = Mathf.Tau * index / points.Length;
                float wobble = 1.0f + Mathf.Sin(angle * 3.0f + gridPosition.X) * 0.10f
                    + Mathf.Cos(angle * 5.0f + gridPosition.Y) * 0.06f;
                points[index] = center + new Vector2(
                    Mathf.Cos(angle) * radius.X * IsometricGrid.TileWidth * 0.5f * wobble,
                    Mathf.Sin(angle) * radius.Y * IsometricGrid.TileHeight * 0.5f * wobble);
            }

            DrawColoredPolygon(points, color);
        }
    }

    public void ToggleRuntimeGrid()
    {
        if (Engine.IsEditorHint())
        {
            return;
        }

        _runtimeGridVisible = !_runtimeGridVisible;
        QueueRedraw();
    }
}
