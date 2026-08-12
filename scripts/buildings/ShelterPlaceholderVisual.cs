using Godot;

namespace AshwoodCounty.Buildings;

[Tool]
public partial class ShelterPlaceholderVisual : Node2D
{
    public override void _Draw()
    {
        DrawColoredPolygon([new Vector2(-92, -53), new Vector2(0, -92), new Vector2(94, -51), new Vector2(0, -8)], new Color("#40594c"));
        DrawColoredPolygon([new Vector2(-82, -48), new Vector2(0, -8), new Vector2(0, 28), new Vector2(-82, -10)], new Color("#a67845"));
        DrawColoredPolygon([new Vector2(0, -8), new Vector2(84, -47), new Vector2(84, -10), new Vector2(0, 28)], new Color("#795234"));
        DrawRect(new Rect2(-12, -5, 24, 33), new Color("#3d3026"));
        DrawRect(new Rect2(-58, -31, 22, 18), new Color("#b9d0c2"));
        DrawLine(new Vector2(-47, -31), new Vector2(-47, -13), new Color("#50665c"), 2);
        DrawLine(new Vector2(-58, -22), new Vector2(-36, -22), new Color("#50665c"), 2);
        DrawLine(new Vector2(-98, -3), new Vector2(0, 43), new Color(0.12f, 0.16f, 0.12f, 0.35f), 10);
        DrawLine(new Vector2(0, 43), new Vector2(96, -2), new Color(0.12f, 0.16f, 0.12f, 0.35f), 10);
    }
}
