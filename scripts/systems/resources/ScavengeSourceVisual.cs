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

        if (!Engine.IsEditorHint() && !_source.IsDepleted)
        {
            float glowAlpha = _source.IsHovered ? 0.26f : 0.13f;
            DrawSetTransform(Vector2.Zero, 0, new Vector2(1.14f, 1.14f));
            DrawPolygon(
                [new(-37, -9), new(0, -22), new(37, -9), new(37, 7), new(0, 21), new(-37, 7)],
                [new Color(1f, 1f, 1f, glowAlpha)]);
            DrawSetTransform(Vector2.Zero, 0, Vector2.One);
        }

        DrawPolygon([new(-37, -9), new(0, -22), new(37, -9), new(0, 5)], [body]);
        DrawPolygon([new(-37, -9), new(0, 5), new(0, 21), new(-37, 7)], [body.Darkened(.22f)]);
        DrawPolygon([new(0, 5), new(37, -9), new(37, 7), new(0, 21)], [body.Darkened(.38f)]);
        DrawLine(new(-21, -12), new(15, 1), new Color("#b5a16b"), 3);
        DrawCircle(new Vector2(24, -10), 4, _source.LootType == ResourceType.Medicine ? new Color("#b74e54") : new Color("#c3a84d"));

        if (!Engine.IsEditorHint() && _source.IsClaimed && !_source.IsDepleted)
        {
            float pulse = 0.5f + 0.5f * Mathf.Sin((float)Time.GetTicksMsec() / 220.0f);
            Color shimmer = new(1f, 1f, 1f, 0.035f + 0.035f * pulse);
            DrawPolygon([new(-37, -9), new(0, -22), new(37, -9), new(0, 5)], [shimmer]);
            DrawPolygon([new(-37, -9), new(0, 5), new(0, 21), new(-37, 7)], [shimmer]);
            DrawPolygon([new(0, 5), new(37, -9), new(37, 7), new(0, 21)], [shimmer]);
        }

        if (!Engine.IsEditorHint() && _source.IsDesignatedForScavenging)
            DrawPolyline(Ellipse(43, 16), new Color("#e6b955"), 2, true);
    }

    private static Vector2[] Ellipse(float x, float y)
    {
        Vector2[] points = new Vector2[33];
        for (int i = 0; i < points.Length; i++) { float angle = Mathf.Tau * i / 32; points[i] = new(Mathf.Cos(angle) * x, Mathf.Sin(angle) * y); }
        return points;
    }
}
