using Godot;

namespace AshwoodCounty.Resources;

[Tool]
public partial class ResourceTreeVisual : Node2D
{
    private static readonly string[] TreeTextures =
    [
        "res://assets/art/environment/vegetation/oak_01.png",
        "res://assets/art/environment/vegetation/pine_01.png",
        "res://assets/art/environment/vegetation/young_tree_01.png"
    ];
    private const string StumpTexture = "res://assets/art/resources/stump_01.png";
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

        if (!Engine.IsEditorHint() && _resource.IsDesignatedForChop)
        {
            DrawDesignationIndicator();
        }

        if (!Engine.IsEditorHint() && _resource.DisplayedHarvestProgress > 0)
        {
            DrawProgress(_resource.DisplayedHarvestProgress);
        }
    }

    private void DrawTree()
    {
        int variation = Mathf.Abs(_resource.GetIndex()) % TreeTextures.Length;
        float scale = variation == 2 ? 0.40f : 0.42f;
        DrawGroundedTexture(TreeTextures[variation], scale);
    }

    private void DrawStump()
    {
        DrawGroundedTexture(StumpTexture, 0.34f);
    }

    private void DrawGroundedTexture(string path, float scale)
    {
        Texture2D texture = GD.Load<Texture2D>(path);
        Vector2 size = texture.GetSize() * scale;
        DrawTextureRect(texture, new Rect2(new Vector2(-size.X * 0.5f, -size.Y), size), false);
    }

    private void DrawTargetIndicator()
    {
        Vector2[] outline = CreateEllipsePoints(30, 11, 32, true);
        DrawPolyline(outline, new Color("#f4c95d"), 3, true);
    }

    private void DrawDesignationIndicator()
    {
        DrawCircle(new Vector2(0, -225), 9, new Color(0.95f, 0.68f, 0.22f, 0.92f));
        DrawLine(new Vector2(-4, -229), new Vector2(4, -221), new Color("#4a2f17"), 2.5f);
        DrawLine(new Vector2(4, -229), new Vector2(-4, -221), new Color("#4a2f17"), 2.5f);
        Vector2[] outline = CreateEllipsePoints(32, 12, 32, true);
        DrawPolyline(outline, new Color(0.95f, 0.68f, 0.22f, 0.78f), 2, true);
    }

    private void DrawProgress(float progress)
    {
        DrawRect(new Rect2(-28, -224, 56, 8), new Color(0.04f, 0.06f, 0.04f, 0.85f));
        DrawRect(new Rect2(-26, -222, 52 * progress, 4), new Color("#efb74d"));
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
