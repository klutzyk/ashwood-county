using Godot;

namespace AshwoodCounty.World;

[Tool]
public partial class PlaceholderObject : Node2D, IGridOccupant
{
    public enum PlaceholderKind
    {
        Tree,
        Survivor,
        Building,
        ResourcePile
    }

    private PlaceholderKind _kind;
    private Vector2 _gridPosition;

    [Export]
    public PlaceholderKind Kind
    {
        get => _kind;
        set
        {
            _kind = value;
            QueueRedraw();
        }
    }

    [Export]
    public Vector2 GridPosition
    {
        get => _gridPosition;
        set
        {
            _gridPosition = value;
            UpdateRenderedPosition();
        }
    }

    [Export] public Vector2I Footprint { get; set; } = Vector2I.One;
    public Vector2I OccupancyOrigin => new(Mathf.FloorToInt(GridPosition.X), Mathf.FloorToInt(GridPosition.Y));
    public Vector2I OccupancyFootprint => Footprint;

    public override void _Ready()
    {
        UpdateRenderedPosition();
        YSortEnabled = true;
        QueueRedraw();
        if (!Engine.IsEditorHint() && Kind != PlaceholderKind.Survivor)
        {
            AddToGroup(GridOccupancy.OccupantGroup);
        }
    }

    private void UpdateRenderedPosition()
    {
        Vector2 projectedPosition = IsometricGrid.GridToScreen(GridPosition + new Vector2(0.5f, 0.5f));
        if (!Position.IsEqualApprox(projectedPosition))
        {
            Position = projectedPosition;
        }
    }

    public override void _Draw()
    {
        DrawSetTransform(Vector2.Zero);
        DrawEllipseShadow();

        switch (Kind)
        {
            case PlaceholderKind.Tree:
                DrawTree();
                break;
            case PlaceholderKind.Survivor:
                DrawSurvivor();
                break;
            case PlaceholderKind.Building:
                DrawBuilding();
                break;
            case PlaceholderKind.ResourcePile:
                DrawResourcePile();
                break;
        }
    }

    private void DrawEllipseShadow()
    {
        DrawEllipse(new Vector2(0, -2), 24, 8, new Color(0.12f, 0.18f, 0.1f, 0.35f));
    }

    private void DrawTree()
    {
        DrawRect(new Rect2(-6, -55, 12, 55), new Color("#6c4931"));
        DrawCircle(new Vector2(0, -68), 30, new Color("#28633b"));
        DrawCircle(new Vector2(-18, -58), 22, new Color("#347848"));
        DrawCircle(new Vector2(18, -57), 21, new Color("#3f8850"));
        DrawCircle(new Vector2(0, -85), 22, new Color("#4a9856"));
    }

    private void DrawSurvivor()
    {
        DrawLine(new Vector2(-5, -17), new Vector2(-9, 0), new Color("#26343b"), 6);
        DrawLine(new Vector2(5, -17), new Vector2(9, 0), new Color("#26343b"), 6);
        DrawRect(new Rect2(-10, -47, 20, 31), new Color("#d8873e"));
        DrawCircle(new Vector2(0, -57), 10, new Color("#e0ad7d"));
        DrawLine(new Vector2(-10, -40), new Vector2(-18, -22), new Color("#d8873e"), 5);
        DrawLine(new Vector2(10, -40), new Vector2(18, -24), new Color("#d8873e"), 5);
    }

    private void DrawBuilding()
    {
        DrawColoredPolygon([new Vector2(-48, -45), new Vector2(0, -70), new Vector2(48, -45), new Vector2(0, -18)], new Color("#435c51"));
        DrawColoredPolygon([new Vector2(-42, -41), new Vector2(0, -18), new Vector2(0, 4), new Vector2(-42, -17)], new Color("#9a7044"));
        DrawColoredPolygon([new Vector2(0, -18), new Vector2(42, -41), new Vector2(42, -17), new Vector2(0, 4)], new Color("#765238"));
        DrawRect(new Rect2(-8, -20, 16, 24), new Color("#3b3026"));
    }

    private void DrawResourcePile()
    {
        Color wood = new("#80542f");
        for (int row = 0; row < 3; row++)
        {
            for (int item = 0; item < 4 - row; item++)
            {
                Vector2 start = new(-28 + item * 17 + row * 8, -6 - row * 10);
                DrawLine(start, start + new Vector2(20, -10), wood, 7);
                DrawCircle(start, 4, new Color("#c28b50"));
            }
        }
    }

    private void DrawEllipse(Vector2 center, float radiusX, float radiusY, Color color)
    {
        const int pointCount = 24;
        Vector2[] points = new Vector2[pointCount];
        for (int i = 0; i < pointCount; i++)
        {
            float angle = Mathf.Tau * i / pointCount;
            points[i] = center + new Vector2(Mathf.Cos(angle) * radiusX, Mathf.Sin(angle) * radiusY);
        }

        DrawColoredPolygon(points, color);
    }
}
