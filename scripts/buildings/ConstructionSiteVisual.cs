using AshwoodCounty.World;
using Godot;

namespace AshwoodCounty.Buildings;

[Tool]
public partial class ConstructionSiteVisual : Node2D
{
    private ConstructionSite _site = null!;

    public override void _Ready()
    {
        _site = GetParent<ConstructionSite>();
        QueueRedraw();
    }

    public override void _Draw()
    {
        Vector2 anchor = BuildingGridProjection.GetRenderAnchor(_site.GridOrigin, _site.Footprint);
        for (int y = 0; y < _site.Footprint.Y; y++)
        {
            for (int x = 0; x < _site.Footprint.X; x++)
            {
                Vector2[] diamond = IsometricGrid.CellDiamond(_site.GridOrigin + new Vector2I(x, y));
                for (int index = 0; index < diamond.Length; index++)
                {
                    diamond[index] -= anchor;
                }

                DrawColoredPolygon(diamond, new Color("#9a7a55"));
                DrawPolyline([diamond[0], diamond[1], diamond[2], diamond[3], diamond[0]], new Color("#654c35"), 2, true);
            }
        }

        float progress = Engine.IsEditorHint() ? 0.35f : _site.Progress;
        float frameHeight = Mathf.Lerp(20, 76, progress);
        foreach (float x in new[] { -78.0f, -24.0f, 30.0f, 82.0f })
        {
            DrawLine(new Vector2(x, -8), new Vector2(x, -8 - frameHeight), new Color("#79502c"), 7);
        }

        DrawLine(new Vector2(-82, -34), new Vector2(86, -34), new Color("#a16d38"), 6);
        if (progress > 0.45f)
        {
            DrawLine(new Vector2(-75, -72), new Vector2(78, -72), new Color("#a16d38"), 7);
            DrawLine(new Vector2(-70, -8), new Vector2(70, -72), new Color("#84582f"), 4);
        }

        DrawRect(new Rect2(-60, -112, 120, 10), new Color(0.04f, 0.06f, 0.04f, 0.88f));
        DrawRect(new Rect2(-57, -109, 114 * progress, 4), new Color("#efb74d"));
    }
}
