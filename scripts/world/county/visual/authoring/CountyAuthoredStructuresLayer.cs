#nullable enable

using Godot;

namespace AshwoodCounty.World.County.Visual.Authoring;

/// <summary>
/// Cullable, decoration-only county structures. One CanvasItem per county
/// chunk keeps the authored layer inexpensive without turning props into
/// hundreds of individual scene nodes.
/// </summary>
public partial class CountyAuthoredStructuresLayer : Node2D
{
    public override void _Ready()
    {
        ZAsRelative = false;
        ZIndex = -72;

        int columns = Mathf.CeilToInt(CountyCoordinateSpace.Width / (float)CountyCoordinateSpace.ChunkSize);
        int rows = Mathf.CeilToInt(CountyCoordinateSpace.Height / (float)CountyCoordinateSpace.ChunkSize);
        for (int y = 0; y < rows; y++)
        {
            for (int x = 0; x < columns; x++)
            {
                CountyAuthoredStructuresChunk chunk = new() { Name = $"AuthoredStructures_{x}_{y}" };
                chunk.Initialize(new Vector2I(x, y));
                AddChild(chunk);
            }
        }
    }
}
