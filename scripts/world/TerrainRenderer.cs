using Godot;

namespace AshwoodCounty.World;

[Tool]
public partial class TerrainRenderer : Node2D
{
    private static readonly Color Grass = new("#769f52");
    private static readonly Color GrassEdge = new("#567b3d");
    private int _width = IsometricWorld.MapWidth;
    private int _height = IsometricWorld.MapHeight;
    private bool _runtimeGridVisible;
    private Texture2D _regionGround = null!;

    private readonly (string Path, Vector2 Position, float Scale, Color Tint)[] _groundPatches =
    [
        ("res://assets/art/terrain/grass_scatter_01.png", new(5, 5), .72f, new(1, 1, 1, .34f)),
        ("res://assets/art/terrain/grass_scatter_01.png", new(16, 5), .62f, new(1, 1, 1, .28f)),
        ("res://assets/art/terrain/dirt_scatter_01.png", new(30, 6), .70f, new(1, 1, 1, .32f)),
        ("res://assets/art/terrain/leaves_01.png", new(7, 11), .65f, new(1, 1, 1, .34f)),
        ("res://assets/art/terrain/grass_scatter_01.png", new(18, 11), .74f, new(1, 1, 1, .30f)),
        ("res://assets/art/terrain/dirt_scatter_01.png", new(27, 12), .67f, new(1, 1, 1, .32f)),
        ("res://assets/art/terrain/grass_scatter_01.png", new(38, 13), .68f, new(1, 1, 1, .28f)),
        ("res://assets/art/terrain/leaves_01.png", new(5, 20), .66f, new(1, 1, 1, .36f)),
        ("res://assets/art/terrain/grass_scatter_01.png", new(15, 20), .62f, new(1, 1, 1, .28f)),
        ("res://assets/art/terrain/dirt_scatter_01.png", new(25, 22), .72f, new(1, 1, 1, .32f)),
        ("res://assets/art/terrain/gravel_scatter_01.png", new(33, 21), .66f, new(1, 1, 1, .34f)),
        ("res://assets/art/terrain/grass_scatter_01.png", new(9, 29), .74f, new(1, 1, 1, .29f)),
        ("res://assets/art/terrain/mud_scatter_01.png", new(18, 31), .64f, new(1, 1, 1, .30f)),
        ("res://assets/art/terrain/dirt_scatter_01.png", new(29, 30), .72f, new(1, 1, 1, .31f)),
        ("res://assets/art/terrain/leaves_01.png", new(38, 31), .64f, new(1, 1, 1, .35f))
    ];

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
        _regionGround ??= TextureRegistry.Get("res://assets/art/terrain/ashwood_outskirts_ground.png");
        QueueRedraw();
    }

    public override void _Draw()
    {
        Vector2[] terrain = IsometricGrid.ProjectRectangle(Vector2.Zero, new Vector2(_width, _height));
        DrawColoredPolygon(terrain, Grass);
        _regionGround ??= TextureRegistry.Get("res://assets/art/terrain/ashwood_outskirts_ground.png");
        if (_regionGround is not null)
        {
            DrawTexture(_regionGround, new Vector2(-_regionGround.GetWidth() * .5f, 0));
        }
        DrawBroadVariations();
        DrawAccessRoad();
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

    private void DrawAccessRoad()
    {
        Vector2[] centerLine =
        [
            new(-2, 33), new(5, 31), new(11, 29), new(17, 27),
            new(23, 26), new(29, 23), new(35, 19), new(44, 16)
        ];
        DrawRoadRibbon(centerLine, .95f, new Color("#806c4b"));
        DrawRoadRibbon(centerLine, .66f, new Color("#a08759"));

        Vector2[] farmTrack = [new(11, 29), new(10, 24), new(9, 18), new(7, 11), new(3, 5)];
        DrawRoadRibbon(farmTrack, .52f, new Color("#806c4b"));
        DrawRoadRibbon(farmTrack, .31f, new Color("#a08759"));
    }

    private static Vector2[] BuildRibbon(Vector2[] line, float halfWidth)
    {
        Vector2[] polygon = new Vector2[line.Length * 2];
        for (int index = 0; index < line.Length; index++)
        {
            Vector2 tangent = index == 0 ? line[1] - line[0]
                : index == line.Length - 1 ? line[^1] - line[^2]
                : line[index + 1] - line[index - 1];
            Vector2 normal = new Vector2(-tangent.Y, tangent.X).Normalized() * halfWidth;
            polygon[index] = IsometricGrid.GridToScreen(line[index] + normal);
            polygon[polygon.Length - 1 - index] = IsometricGrid.GridToScreen(line[index] - normal);
        }
        return polygon;
    }

    private void DrawRoadRibbon(Vector2[] line, float halfWidth, Color color)
    {
        DrawColoredPolygon(BuildRibbon(line, halfWidth), color);
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
            Texture2D texture = TextureRegistry.Get(path);
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
