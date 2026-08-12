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
