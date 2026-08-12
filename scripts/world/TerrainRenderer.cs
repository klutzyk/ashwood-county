using Godot;

namespace AshwoodCounty.World;

public partial class TerrainRenderer : Node2D
{
    private static readonly Color GrassA = new("#78a857");
    private static readonly Color GrassB = new("#82b45e");
    private static readonly Color GrassEdge = new("#567b3d");
    private int _width;
    private int _height;

    public void Configure(int width, int height)
    {
        _width = width;
        _height = height;
        QueueRedraw();
    }

    public override void _Draw()
    {
        for (int y = 0; y < _height; y++)
        {
            for (int x = 0; x < _width; x++)
            {
                Vector2I cell = new(x, y);
                Vector2[] diamond = IsometricGrid.CellDiamond(cell);
                DrawColoredPolygon(diamond, (x + y) % 2 == 0 ? GrassA : GrassB);
                DrawPolyline([diamond[0], diamond[1], diamond[2], diamond[3], diamond[0]], GrassEdge, 1.0f, true);
            }
        }
    }
}
