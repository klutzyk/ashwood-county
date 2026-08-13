using AshwoodCounty.World;
using Godot;

namespace AshwoodCounty.Resources;

[Tool]
public partial class ScavengeSourceVisual : Node2D
{
    private ScavengeSource _source = null!;

    public override void _Ready() { _source = GetParent<ScavengeSource>(); QueueRedraw(); }

    public override void _Draw()
    {
        bool depleted = !Engine.IsEditorHint() && _source.IsDepleted;
        Color body = depleted ? new Color("#41413a") : new Color("#737263");
        DrawPolygon([new(-37, -9), new(0, -22), new(37, -9), new(0, 5)], [body]);
        DrawPolygon([new(-37, -9), new(0, 5), new(0, 21), new(-37, 7)], [body.Darkened(.22f)]);
        DrawPolygon([new(0, 5), new(37, -9), new(37, 7), new(0, 21)], [body.Darkened(.38f)]);
        DrawLine(new(-21, -12), new(15, 1), new Color("#b5a16b"), 3);
        DrawCircle(new Vector2(24, -10), 4, _source.LootType == ResourceType.Medicine ? new Color("#b74e54") : new Color("#c3a84d"));

        if (!Engine.IsEditorHint() && _source.IsDesignatedForScavenging)
            DrawPolyline(Ellipse(43, 16), new Color("#e6b955"), 2, true);
        if (!Engine.IsEditorHint() && _source.DisplayedSearchProgress > 0)
        {
            DrawRect(new Rect2(-28, -34, 56, 5), new Color("#252820"));
            DrawRect(new Rect2(-27, -33, 54 * _source.DisplayedSearchProgress, 3), new Color("#d1b25a"));
        }
    }

    private static Vector2[] Ellipse(float x, float y)
    {
        Vector2[] points = new Vector2[33];
        for (int i = 0; i < points.Length; i++) { float angle = Mathf.Tau * i / 32; points[i] = new(Mathf.Cos(angle) * x, Mathf.Sin(angle) * y); }
        return points;
    }
}
