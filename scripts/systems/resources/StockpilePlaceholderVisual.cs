using Godot;

namespace AshwoodCounty.Resources;

[Tool]
public partial class StockpilePlaceholderVisual : Node2D
{
    public override void _Draw()
    {
        DrawColoredPolygon([new Vector2(-45, -5), new Vector2(0, -25), new Vector2(45, -5), new Vector2(0, 17)], new Color("#725438"));
        DrawPolyline([new Vector2(-45, -5), new Vector2(0, 17), new Vector2(45, -5)], new Color("#4c3526"), 5, true);

        Color wood = new("#8b5c32");
        for (int row = 0; row < 3; row++)
        {
            for (int item = 0; item < 4 - row; item++)
            {
                Vector2 start = new(-29 + item * 18 + row * 9, -13 - row * 10);
                DrawLine(start, start + new Vector2(22, -10), wood, 7);
                DrawCircle(start, 4, new Color("#c38a4c"));
            }
        }

        DrawRect(new Rect2(30, -55, 5, 47), new Color("#57402e"));
        DrawRect(new Rect2(20, -58, 26, 18), new Color("#d7c091"));
        DrawLine(new Vector2(25, -49), new Vector2(41, -49), new Color("#6f5635"), 2);
    }
}
