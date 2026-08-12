using Godot;

namespace AshwoodCounty.Resources;

[Tool]
public partial class ResourceTreeVisual : Node2D
{
    private HarvestableResource _resource = null!;

    public override void _Ready()
    {
        _resource = GetParent<HarvestableResource>();
        SetProcess(!Engine.IsEditorHint());
        QueueRedraw();
    }

    public override void _Process(double delta)
    {
        if (_resource.IsTargeted || _resource.DisplayedHarvestProgress > 0)
        {
            QueueRedraw();
        }
    }

    public override void _Draw()
    {
        DrawEllipse(new Vector2(0, -2), 24, 8, new Color(0.12f, 0.18f, 0.1f, 0.35f));

        bool depleted = !Engine.IsEditorHint() && _resource.IsDepleted;
        if (depleted)
        {
            DrawStump();
        }
        else
        {
            DrawTree();
        }

        if (!Engine.IsEditorHint() && _resource.IsTargeted)
        {
            DrawTargetIndicator();
        }

        if (!Engine.IsEditorHint() && _resource.DisplayedHarvestProgress > 0)
        {
            DrawProgress(_resource.DisplayedHarvestProgress);
        }
    }

    private void DrawTree()
    {
        DrawRect(new Rect2(-6, -55, 12, 55), new Color("#6c4931"));
        DrawCircle(new Vector2(0, -68), 30, new Color("#28633b"));
        DrawCircle(new Vector2(-18, -58), 22, new Color("#347848"));
        DrawCircle(new Vector2(18, -57), 21, new Color("#3f8850"));
        DrawCircle(new Vector2(0, -85), 22, new Color("#4a9856"));
    }

    private void DrawStump()
    {
        DrawRect(new Rect2(-10, -18, 20, 18), new Color("#67452d"));
        DrawEllipse(new Vector2(0, -18), 10, 4, new Color("#b17a45"));
        DrawLine(new Vector2(-4, -18), new Vector2(4, -18), new Color("#6d472a"), 1.5f);
    }

    private void DrawTargetIndicator()
    {
        Vector2[] outline = CreateEllipsePoints(30, 11, 32, true);
        DrawPolyline(outline, new Color("#f4c95d"), 3, true);
    }

    private void DrawProgress(float progress)
    {
        DrawRect(new Rect2(-28, -121, 56, 8), new Color(0.04f, 0.06f, 0.04f, 0.85f));
        DrawRect(new Rect2(-26, -119, 52 * progress, 4), new Color("#efb74d"));
    }

    private void DrawEllipse(Vector2 center, float radiusX, float radiusY, Color color)
    {
        DrawColoredPolygon(CreateEllipsePoints(radiusX, radiusY, 24, false, center), color);
    }

    private static Vector2[] CreateEllipsePoints(float radiusX, float radiusY, int pointCount, bool close, Vector2 center = default)
    {
        Vector2[] points = new Vector2[pointCount + (close ? 1 : 0)];
        for (int index = 0; index < points.Length; index++)
        {
            float angle = Mathf.Tau * index / pointCount;
            points[index] = center + new Vector2(Mathf.Cos(angle) * radiusX, Mathf.Sin(angle) * radiusY - 2);
        }

        return points;
    }
}
