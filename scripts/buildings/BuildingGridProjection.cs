using AshwoodCounty.World;
using Godot;

namespace AshwoodCounty.Buildings;

public static class BuildingGridProjection
{
    public static Vector2 GetRenderAnchor(Vector2 position, Vector2 footprintSize)
    {
        return IsometricGrid.GridToScreen(position + footprintSize);
    }

    public static Vector2 GetFootprintCenter(Vector2 position, Vector2 footprintSize)
    {
        return position + footprintSize * 0.5f;
    }
}
