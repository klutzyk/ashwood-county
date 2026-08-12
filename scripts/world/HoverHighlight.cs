using Godot;

namespace AshwoodCounty.World;

public partial class HoverHighlight : Node2D
{
    private Vector2I _cell = new(-1, -1);

    public void SetHoveredCell(Vector2I cell)
    {
        _cell = cell;
    }
}
