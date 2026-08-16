using Godot;
using AshwoodCounty.World;

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
        if (_resource.IsTargeted || _resource.DisplayedHarvestProgress > 0 || _resource.IsHovered || _resource.IsWorkHighlighted)
        {
            QueueRedraw();
        }
    }

    public override void _Draw()
    {
        bool depleted = !Engine.IsEditorHint() && _resource.IsDepleted;
        int variation = Mathf.Abs(_resource.GetIndex()) % TreeTextures.Length;
        float scale = variation == 2 ? 0.40f : 0.42f;
        string texturePath = depleted ? StumpTexture : TreeTextures[variation];
        float textureScale = depleted ? 0.34f : scale;

        if (!Engine.IsEditorHint() && !depleted)
        {
            if (_resource.IsWorkHighlighted && _resource.IsHarvestable)
            {
                DrawSilhouetteGlow(texturePath, textureScale, 0.34f);
            }
            else if (_resource.IsHovered)
            {
                DrawSilhouetteGlow(texturePath, textureScale, 0.20f);
            }
        }

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

        if (!Engine.IsEditorHint() && _resource.IsDesignatedForHarvest)
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
        DrawGroundedTexture(path, scale, Colors.White);
    }

    private void DrawGroundedTexture(string path, float scale, Color tint)
    {
        Texture2D texture = TextureRegistry.Get(path);
        Vector2 size = texture.GetSize() * scale;
        DrawTextureRect(texture, new Rect2(new Vector2(-size.X * 0.5f, -size.Y), size), false, tint);
    }

    private void DrawTargetIndicator()
    {
        Vector2[] outline = CreateEllipsePoints(30, 11, 32, true);
        DrawPolyline(outline, new Color("#f4c95d"), 3, true);
    }

    /// <summary>
    /// A soft white rim that traces the object's own silhouette: a slightly
    /// enlarged white copy behind the sprite, then a tighter pass, so the
    /// highlight reads as the object glowing rather than a ground marker.
    /// </summary>
    private void DrawSilhouetteGlow(string path, float scale, float alpha)
    {
        float pulse = 0.86f + 0.14f * Mathf.Sin((float)Time.GetTicksMsec() / 520.0f);
        DrawGroundedTexture(path, scale * 1.12f, new Color(1f, 1f, 1f, alpha * pulse));
        DrawGroundedTexture(path, scale * 1.05f, new Color(1f, 1f, 1f, alpha * 0.5f * pulse));
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
