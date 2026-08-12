using Godot;

namespace AshwoodCounty.Resources;

public partial class ResourceDepositFeedback : Node2D
{
    private const float Lifetime = 1.0f;
    private string _text = string.Empty;
    private float _age;

    public void Initialize(ResourceType resourceType, int amount)
    {
        _text = $"+{amount} {resourceType}";
        Position = new Vector2(-28, -72);
        QueueRedraw();
    }

    public override void _Process(double delta)
    {
        _age += (float)delta;
        Position += Vector2.Up * 18 * (float)delta;
        Modulate = new Color(1, 1, 1, 1 - _age / Lifetime);
        if (_age >= Lifetime)
        {
            QueueFree();
        }
    }

    public override void _Draw()
    {
        DrawString(ThemeDB.FallbackFont, Vector2.Zero, _text, HorizontalAlignment.Left, -1, 16, new Color("#f4d06f"));
    }
}
