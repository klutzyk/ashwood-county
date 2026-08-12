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
