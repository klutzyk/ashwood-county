using Godot;

namespace AshwoodCounty.World;

[Tool]
public partial class ArtDecoration : Node2D
{
    private Vector2 _gridPosition;

    [Export] public Texture2D Texture { get; set; }
    [Export] public float VisualScale { get; set; } = 0.35f;
    [Export] public Vector2 Anchor { get; set; } = new(0.5f, 1.0f);
    [Export] public Color Tint { get; set; } = Colors.White;
    [Export] public bool BlocksMovement { get; set; }
    [Export] public bool OccludesView { get; set; }

    [Export]
    public Vector2 GridPosition
    {
        get => _gridPosition;
        set
        {
            _gridPosition = value;
            Position = IsometricGrid.GridToScreen(value);
            QueueRedraw();
        }
    }

    public override void _Ready()
    {
        Position = IsometricGrid.GridToScreen(GridPosition);
        if (!Engine.IsEditorHint() && GetTree().GetFirstNodeInGroup(WorldNavigationService.GroupName) is WorldNavigationService navigationService && BlocksMovement)
        {
            navigationService.RegisterObstacle(new WorldFootprint(GridPosition - Vector2.One * 0.45f, Vector2.One * 0.9f), this, allowTraversalInside: false);
        }
        QueueRedraw();
    }

    public override void _ExitTree()
    {
        if (IsInsideTree() && GetTree().GetFirstNodeInGroup(WorldNavigationService.GroupName) is WorldNavigationService navigationService)
        {
            navigationService.UnregisterObstacle(this);
        }
    }

    public override void _Draw()
    {
        if (Texture is null)
        {
            return;
        }

        Vector2 size = Texture.GetSize() * VisualScale;
        DrawTextureRect(Texture, new Rect2(-size * Anchor, size), false, Tint);
    }
}
