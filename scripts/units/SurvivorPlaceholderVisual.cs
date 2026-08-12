using Godot;

namespace AshwoodCounty.Units;

[Tool]
public partial class SurvivorPlaceholderVisual : Node2D
{
    private Color _shirtColor = new("#d8873e");

    [Export]
    public Color ShirtColor
    {
        get => _shirtColor;
        set
        {
            _shirtColor = value;
            QueueRedraw();
        }
    }

    public override void _Draw()
    {
        DrawEllipse(new Vector2(0, -2), 20, 7, new Color(0.12f, 0.18f, 0.1f, 0.35f));
        DrawLine(new Vector2(-5, -17), new Vector2(-9, 0), new Color("#26343b"), 6);
        DrawLine(new Vector2(5, -17), new Vector2(9, 0), new Color("#26343b"), 6);
        DrawRect(new Rect2(-10, -47, 20, 31), ShirtColor);
        DrawCircle(new Vector2(0, -57), 10, new Color("#e0ad7d"));
        DrawLine(new Vector2(-10, -40), new Vector2(-18, -22), ShirtColor, 5);
        DrawLine(new Vector2(10, -40), new Vector2(18, -24), ShirtColor, 5);

        if (!Engine.IsEditorHint() && GetParent() is Survivor survivor && survivor.CarriedAmount > 0)
        {
            DrawCarriedWood(survivor.CarriedAmount);
        }
    }

    private void DrawCarriedWood(int amount)
    {
        DrawCircle(new Vector2(17, -29), 10, new Color(0.12f, 0.1f, 0.07f, 0.55f));
        Color wood = new("#a66d35");
        for (int index = 0; index < 3; index++)
        {
            Vector2 start = new(9 + index * 5, -34 + index * 2);
            DrawLine(start, start + new Vector2(12, -5), wood, 4);
        }

        DrawString(ThemeDB.FallbackFont, new Vector2(22, -18), amount.ToString(), HorizontalAlignment.Left, -1, 11, Colors.White);
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
