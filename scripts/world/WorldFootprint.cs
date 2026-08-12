using Godot;

namespace AshwoodCounty.World;

/// <summary>
/// A continuous, axis-aligned footprint in simulation/grid space. The hidden
/// spatial grid indexes its bounds, but placement validity uses this shape.
/// </summary>
public readonly record struct WorldFootprint(Vector2 Position, Vector2 Size)
{
    public Rect2 Bounds => new(Position, Size);
    public Vector2 Center => Position + Size * 0.5f;
    public bool IsValid => Size.X > 0 && Size.Y > 0;

    public bool Overlaps(WorldFootprint other)
    {
        return Bounds.Intersects(other.Bounds, false);
    }
}
