using Godot;

namespace AshwoodCounty.World;

[Tool]
public partial class TerrainRenderer : Node2D
{
    private static readonly Color Grass = new("#7eae5b");
    private static readonly Color GrassEdge = new("#567b3d");
    private int _width = IsometricWorld.MapWidth;
    private int _height = IsometricWorld.MapHeight;
    private bool _runtimeGridVisible;

    private readonly (string Path, Vector2 Position, float Scale, Color Tint)[] _groundPatches =
    [
        ("res://assets/art/terrain/grass_scatter_01.png", new Vector2(4.6f, 4.1f), 0.76f, new Color(1, 1, 1, 0.42f)),
        ("res://assets/art/terrain/grass_scatter_01.png", new Vector2(18.3f, 5.8f), 0.62f, new Color(0.92f, 1, 0.88f, 0.34f)),
        ("res://assets/art/terrain/dirt_scatter_01.png", new Vector2(11.7f, 8.3f), 0.72f, new Color(1, 1, 1, 0.55f)),
        ("res://assets/art/terrain/leaves_01.png", new Vector2(24.2f, 9.6f), 0.60f, new Color(1, 1, 1, 0.38f)),
        ("res://assets/art/terrain/grass_scatter_01.png", new Vector2(7.4f, 13.8f), 0.58f, new Color(1, 1, 1, 0.34f)),
        ("res://assets/art/terrain/dirt_scatter_01.png", new Vector2(15.5f, 15.1f), 0.92f, new Color(1, 1, 1, 0.48f)),
        ("res://assets/art/terrain/gravel_scatter_01.png", new Vector2(21.4f, 16.7f), 0.54f, new Color(1, 1, 1, 0.35f)),
        ("res://assets/art/terrain/grass_scatter_01.png", new Vector2(4.7f, 19.2f), 0.70f, new Color(0.9f, 1, 0.86f, 0.38f)),
        ("res://assets/art/terrain/mud_scatter_01.png", new Vector2(12.8f, 21.3f), 0.56f, new Color(1, 1, 1, 0.34f)),
        ("res://assets/art/terrain/dirt_scatter_01.png", new Vector2(23.1f, 23.6f), 0.68f, new Color(1, 1, 1, 0.42f)),
        ("res://assets/art/terrain/leaves_01.png", new Vector2(8.3f, 26.0f), 0.48f, new Color(1, 1, 1, 0.30f)),
        ("res://assets/art/terrain/grass_scatter_01.png", new Vector2(19.2f, 27.1f), 0.64f, new Color(1, 1, 1, 0.32f))
    ];

    private readonly (Vector2 Position, Vector2 Radius, Color Color)[] _broadVariations =
    [
        (new Vector2(7.5f, 7.0f), new Vector2(7.5f, 4.2f), new Color(0.12f, 0.24f, 0.07f, 0.16f)),
        (new Vector2(23.8f, 6.0f), new Vector2(6.2f, 4.5f), new Color(0.20f, 0.28f, 0.09f, 0.13f)),
        (new Vector2(8.0f, 23.5f), new Vector2(6.8f, 4.8f), new Color(0.22f, 0.17f, 0.07f, 0.12f)),
        (new Vector2(23.0f, 23.0f), new Vector2(7.2f, 5.2f), new Color(0.10f, 0.22f, 0.08f, 0.15f)),
        (new Vector2(14.5f, 14.5f), new Vector2(8.5f, 5.5f), new Color(0.39f, 0.31f, 0.13f, 0.08f))
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
        Vector2[] terrain = IsometricGrid.ProjectRectangle(Vector2.Zero, new Vector2(_width, _height));
        DrawColoredPolygon(terrain, Grass);
        DrawBroadVariations();
        DrawGroundPatches();

        if (!IsGridVisible)
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

    private void DrawGroundPatches()
    {
        foreach ((string path, Vector2 gridPosition, float scale, Color tint) in _groundPatches)
        {
            Texture2D texture = GD.Load<Texture2D>(path);
            Vector2 size = texture.GetSize() * scale;
            Vector2 position = IsometricGrid.GridToScreen(gridPosition);
            DrawTextureRect(texture, new Rect2(position - size * 0.5f, size), false, tint);
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
