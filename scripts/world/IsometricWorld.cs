using AshwoodCounty.Camera;
using Godot;

namespace AshwoodCounty.World;

public partial class IsometricWorld : Node2D
{
    public const int MapWidth = 30;
    public const int MapHeight = 30;

    private TerrainRenderer _terrain = null!;
    private HoverHighlight _hover = null!;
    private StrategyCamera _camera = null!;

    public Vector2I HoveredCell { get; private set; } = new(-1, -1);
    public float CameraZoom => _camera.Zoom.X;

    public Vector2 ScreenToGridPosition(Vector2 screenPosition)
    {
        Vector2 localWorldPosition = GetGlobalTransformWithCanvas().AffineInverse() * screenPosition;
        return IsometricGrid.ScreenToGrid(localWorldPosition);
    }

    public override void _Ready()
    {
        _terrain = GetNode<TerrainRenderer>("Terrain");
        _hover = GetNode<HoverHighlight>("HoverHighlight");
        _camera = GetNode<StrategyCamera>("StrategyCamera");

        _terrain.Configure(MapWidth, MapHeight);
        _camera.ConfigureBounds(CalculateMapBounds());
    }

    public override void _Process(double delta)
    {
        Vector2 mouseWorld = GetGlobalMousePosition();
        Vector2I cell = IsometricGrid.ScreenToCell(mouseWorld);
        HoveredCell = IsCellInBounds(cell) ? cell : new Vector2I(-1, -1);
        _hover.SetHoveredCell(HoveredCell);
    }

    public override void _UnhandledInput(InputEvent inputEvent)
    {
        if (inputEvent is InputEventKey keyEvent && keyEvent.Pressed && !keyEvent.Echo
            && keyEvent.Keycode == Key.G)
        {
            _terrain.ToggleRuntimeGrid();
            GetViewport().SetInputAsHandled();
        }
    }

    public static bool IsCellInBounds(Vector2I cell)
    {
        return cell.X >= 0 && cell.Y >= 0 && cell.X < MapWidth && cell.Y < MapHeight;
    }

    public static bool IsGridPositionInBounds(Vector2 position)
    {
        return position.X >= 0 && position.Y >= 0 && position.X < MapWidth && position.Y < MapHeight;
    }

    private Rect2 CalculateMapBounds()
    {
        Vector2 top = IsometricGrid.GridToScreen(Vector2.Zero);
        Vector2 right = IsometricGrid.GridToScreen(new Vector2(MapWidth, 0));
        Vector2 bottom = IsometricGrid.GridToScreen(new Vector2(MapWidth, MapHeight));
        Vector2 left = IsometricGrid.GridToScreen(new Vector2(0, MapHeight));

        float minX = Mathf.Min(Mathf.Min(top.X, right.X), Mathf.Min(bottom.X, left.X));
        float maxX = Mathf.Max(Mathf.Max(top.X, right.X), Mathf.Max(bottom.X, left.X));
        float minY = Mathf.Min(Mathf.Min(top.Y, right.Y), Mathf.Min(bottom.Y, left.Y));
        float maxY = Mathf.Max(Mathf.Max(top.Y, right.Y), Mathf.Max(bottom.Y, left.Y));
        return new Rect2(minX, minY, maxX - minX, maxY - minY);
    }
}
