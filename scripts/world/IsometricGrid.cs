using Godot;

namespace AshwoodCounty.World;

/// <summary>
/// Stateless projection helpers. Gameplay positions stay in grid/world space;
/// only rendering and mouse picking use projected canvas coordinates.
/// </summary>
public static class IsometricGrid
{
    public const float TileWidth = 96.0f;
    public const float TileHeight = 48.0f;

    public static Vector2 GridToScreen(Vector2 gridPosition)
    {
        return new Vector2(
            (gridPosition.X - gridPosition.Y) * TileWidth * 0.5f,
            (gridPosition.X + gridPosition.Y) * TileHeight * 0.5f);
    }

    public static Vector2 ScreenToGrid(Vector2 screenPosition)
    {
        return new Vector2(
            screenPosition.X / TileWidth + screenPosition.Y / TileHeight,
            screenPosition.Y / TileHeight - screenPosition.X / TileWidth);
    }

    public static Vector2I ScreenToCell(Vector2 screenPosition)
    {
        Vector2 gridPosition = ScreenToGrid(screenPosition);
        return new Vector2I(Mathf.FloorToInt(gridPosition.X), Mathf.FloorToInt(gridPosition.Y));
    }

    public static Vector2[] CellDiamond(Vector2I cell)
    {
        Vector2 top = GridToScreen(cell);
        Vector2 right = GridToScreen(cell + Vector2I.Right);
        Vector2 bottom = GridToScreen(cell + Vector2I.One);
        Vector2 left = GridToScreen(cell + Vector2I.Down);
        return [top, right, bottom, left];
    }
}
