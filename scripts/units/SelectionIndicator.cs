using Godot;

namespace AshwoodCounty.Units;

public partial class SelectionIndicator : Node2D
{
    public override void _Draw()
    {
        const int pointCount = 32;
        Vector2[] outline = new Vector2[pointCount + 1];
        for (int i = 0; i <= pointCount; i++)
        {
            float angle = Mathf.Tau * i / pointCount;
            outline[i] = new Vector2(Mathf.Cos(angle) * 27, Mathf.Sin(angle) * 10 - 2);
        }

        DrawColoredPolygon(outline[..pointCount], new Color(0.38f, 0.95f, 0.46f, 0.18f));
        DrawPolyline(outline, new Color("#7df28b"), 3.0f, true);
    }
}
