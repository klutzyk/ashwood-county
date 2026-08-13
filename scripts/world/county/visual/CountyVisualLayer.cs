#nullable enable

using Godot;

namespace AshwoodCounty.World.County.Visual;

/// <summary>
/// County art is retained in one CanvasItem per coordinate chunk. This is a
/// deliberate rendering boundary: Godot can cull remote art while the world
/// remains one continuous gameplay space.
/// </summary>
public partial class CountyVisualLayer : Node2D
{
    public bool DrawLocationLabels { get; init; }

    public override void _Ready()
    {
        ZAsRelative = false;
        ZIndex = -100;

        AddChild(new CountyWaterLayer { Name = "AnimatedWater" });

        int columns = Mathf.CeilToInt(CountyCoordinateSpace.Width / (float)CountyCoordinateSpace.ChunkSize);
        int rows = Mathf.CeilToInt(CountyCoordinateSpace.Height / (float)CountyCoordinateSpace.ChunkSize);
        for (int y = 0; y < rows; y++)
        {
            for (int x = 0; x < columns; x++)
            {
                CountyVisualChunk visual = new()
                {
                    Name = $"Landscape_{x}_{y}",
                    DrawLocationLabels = DrawLocationLabels
                };
                visual.Initialize(new Vector2I(x, y));
                AddChild(visual);
            }
        }
    }
}
