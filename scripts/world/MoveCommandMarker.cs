using Godot;

namespace AshwoodCounty.World;

public partial class MoveCommandMarker : Node2D
{
    private const float Lifetime = 0.65f;
    private float _age;

    public void Initialize(Vector2 gridPosition)
    {
        Position = IsometricGrid.GridToScreen(gridPosition);
    }

    public override void _Process(double delta)
    {
        _age += (float)delta;
        if (_age >= Lifetime)
        {
            QueueFree();
            return;
        }

        QueueRedraw();
    }

    public override void _Draw()
    {
        float progress = _age / Lifetime;
        float radius = Mathf.Lerp(12.0f, 34.0f, progress);
        float alpha = 1.0f - progress;
        const int pointCount = 32;
        Vector2[] outline = new Vector2[pointCount + 1];
        for (int i = 0; i <= pointCount; i++)
        {
            float angle = Mathf.Tau * i / pointCount;
            outline[i] = new Vector2(Mathf.Cos(angle) * radius, Mathf.Sin(angle) * radius * 0.5f);
        }

        DrawPolyline(outline, new Color(1.0f, 0.9f, 0.35f, alpha), 3.0f, true);
    }
}
