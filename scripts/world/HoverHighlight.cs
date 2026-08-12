using Godot;

namespace AshwoodCounty.World;

public partial class HoverHighlight : Node2D
{
    private Vector2I _cell = new(-1, -1);

    public void SetHoveredCell(Vector2I cell)
    {
        if (cell == _cell)
        {
            return;
        }

        _cell = cell;
        QueueRedraw();
    }

    public override void _Draw()
    {
        if (_cell.X < 0)
        {
            return;
        }

        Vector2[] diamond = IsometricGrid.CellDiamond(_cell);
        DrawColoredPolygon(diamond, new Color(1.0f, 0.88f, 0.28f, 0.22f));
        DrawPolyline([diamond[0], diamond[1], diamond[2], diamond[3], diamond[0]], new Color("#ffe477"), 3.0f, true);
    }
}
