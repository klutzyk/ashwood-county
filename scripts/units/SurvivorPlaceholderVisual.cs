using Godot;

namespace AshwoodCounty.Units;

public partial class SurvivorPlaceholderVisual : Node2D
{
    [Export] public Color ShirtColor { get; set; } = new("#d8873e");

    public override void _Draw()
    {
        DrawEllipse(new Vector2(0, -2), 20, 7, new Color(0.12f, 0.18f, 0.1f, 0.35f));
        DrawLine(new Vector2(-5, -17), new Vector2(-9, 0), new Color("#26343b"), 6);
        DrawLine(new Vector2(5, -17), new Vector2(9, 0), new Color("#26343b"), 6);
        DrawRect(new Rect2(-10, -47, 20, 31), ShirtColor);
        DrawCircle(new Vector2(0, -57), 10, new Color("#e0ad7d"));
        DrawLine(new Vector2(-10, -40), new Vector2(-18, -22), ShirtColor, 5);
        DrawLine(new Vector2(10, -40), new Vector2(18, -24), ShirtColor, 5);
    }

    private void DrawEllipse(Vector2 center, float radiusX, float radiusY, Color color)
    {
        const int pointCount = 24;
        Vector2[] points = new Vector2[pointCount];
        for (int i = 0; i < pointCount; i++)
        {
            float angle = Mathf.Tau * i / pointCount;
            points[i] = center + new Vector2(Mathf.Cos(angle) * radiusX, Mathf.Sin(angle) * radiusY);
        }

        DrawColoredPolygon(points, color);
    }
}
