using AshwoodCounty.World;
using Godot;

namespace AshwoodCounty.Buildings;

public static class BuildingGridProjection
{
    public static Vector2 GetRenderAnchor(Vector2I origin, Vector2I footprint)
    {
        return IsometricGrid.GridToScreen(origin + footprint);
    }

    public static Vector2 GetFootprintCenter(Vector2I origin, Vector2I footprint)
    {
        return new Vector2(origin.X + footprint.X * 0.5f, origin.Y + footprint.Y * 0.5f);
    }
}
