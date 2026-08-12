using Godot;

namespace AshwoodCounty.World;

/// <summary>Lightweight, non-interactive region dressing shared by runtime and editor preview.</summary>
[Tool]
public partial class RegionDecorationRenderer : Node2D
{
    private readonly record struct Decoration(string Texture, Vector2 GridPosition, float Scale, Color Tint);

    private static readonly Decoration[] Decorations =
    [
        // Northwest woodland floor and old woodcutting spot.
        D("leaves_01", 3.5f, 7.5f, .72f, .68f), D("leaves_01", 6.2f, 9.0f, .62f, .58f),
        V("fern_01", 2.8f, 6.0f, .30f), V("fern_01", 5.1f, 8.1f, .34f), V("bush_01", 7.0f, 6.0f, .35f),
        R("fallen_log_01", 8.8f, 8.5f, .38f), R("stump_01", 9.8f, 9.2f, .31f), R("wood_stack_01", 7.8f, 9.6f, .28f),

        // Western meadow and broken fence line.
        V("flowers_01", 6.5f, 18.0f, .27f), V("grass_clump_01", 8.2f, 20.2f, .25f),
        P("fence_01", 4.8f, 23.8f, .31f), P("fence_01", 6.1f, 23.2f, .31f), P("fence_01", 7.4f, 22.6f, .31f),
        R("mossy_rock_01", 5.8f, 25.1f, .29f),

        // Settlement clearing: restrained arrival traces.
        V("grass_clump_01", 15.1f, 17.0f, .23f), V("flowers_01", 27.5f, 18.2f, .24f),
        V("grass_clump_01", 28.2f, 25.8f, .22f), R("fallen_log_01", 27.7f, 27.0f, .31f),

        // East roadside pull-off / abandoned storage landmark.
        G("gravel_scatter_01", 34.2f, 17.5f, .82f, .65f), R("wood_stack_02", 35.0f, 16.8f, .31f),
        P("fence_01", 36.2f, 15.9f, .29f), P("fence_01", 37.4f, 15.3f, .29f),
        V("dead_tree_01", 38.2f, 18.3f, .30f),

        // Southern fallen-tree clearing.
        R("fallen_log_01", 29.7f, 32.0f, .40f), R("stump_01", 31.1f, 31.4f, .32f),
        V("fern_01", 28.6f, 33.0f, .32f), V("bush_01", 32.2f, 32.2f, .34f),

        // Sparse forest-edge understory; deliberately clear of the camp core.
        V("bush_01", 12.0f, 5.5f, .34f), V("fern_01", 17.0f, 4.2f, .30f),
        V("grass_clump_01", 23.0f, 5.5f, .23f), V("flowers_01", 29.3f, 6.3f, .24f),
        V("fern_01", 36.5f, 8.0f, .33f), V("bush_01", 39.0f, 11.2f, .34f),
        V("fern_01", 3.0f, 30.0f, .32f), V("bush_01", 8.5f, 34.5f, .36f),
        V("grass_clump_01", 17.2f, 35.0f, .24f), V("flowers_01", 23.8f, 34.0f, .24f),
        V("fern_01", 37.5f, 29.0f, .33f), V("bush_01", 40.0f, 34.0f, .36f),
        R("rock_cluster_01", 3.7f, 14.0f, .29f), R("mossy_rock_01", 39.0f, 25.5f, .30f)
    ];

    public override void _Ready() => QueueRedraw();

    public override void _Draw()
    {
        foreach (Decoration decoration in Decorations)
        {
            Texture2D texture = GD.Load<Texture2D>(decoration.Texture);
            Vector2 size = texture.GetSize() * decoration.Scale;
            Vector2 at = IsometricGrid.GridToScreen(decoration.GridPosition);
            DrawTextureRect(texture, new Rect2(at - new Vector2(size.X * .5f, size.Y), size), false, decoration.Tint);
        }
    }

    private static Decoration V(string name, float x, float y, float scale) =>
        new($"res://assets/art/environment/vegetation/{name}.png", new(x, y), scale, Colors.White);
    private static Decoration P(string name, float x, float y, float scale) =>
        new($"res://assets/art/environment/props/{name}.png", new(x, y), scale, Colors.White);
    private static Decoration R(string name, float x, float y, float scale) =>
        new($"res://assets/art/{(name.Contains("rock") ? "environment/rocks" : "resources")}/{name}.png", new(x, y), scale, Colors.White);
    private static Decoration D(string name, float x, float y, float scale, float alpha) =>
        new($"res://assets/art/terrain/{name}.png", new(x, y), scale, new Color(1, 1, 1, alpha));
    private static Decoration G(string name, float x, float y, float scale, float alpha) => D(name, x, y, scale, alpha);
}
