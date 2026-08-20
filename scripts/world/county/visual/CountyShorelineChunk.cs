#nullable enable

using Godot;

namespace AshwoodCounty.World.County.Visual;

/// <summary>
/// Bank artwork along the lake, the river, the creek and the still ponds.
///
/// It lives on its own CanvasItem because the animated water surfaces render
/// between the landscape chunk and the actors: dressing drawn with the rest of
/// the landscape is simply covered by the water it is meant to be edging. Sat
/// just above the water, reeds and shore rocks break the waterline so open
/// water meets land through a silted margin instead of a hard polygon edge.
/// </summary>
internal partial class CountyShorelineChunk : Node2D
{
    private const string WaterProps = "res://assets/art/water/props/";

    private static readonly Vector2[] LakeOutline =
        [.. CountyMacroLayout.BlackwaterLakeOutline, CountyMacroLayout.BlackwaterLakeOutline[0]];

    /// <summary>Matches the still ponds built by <see cref="CountyWaterLayer"/>.</summary>
    private static readonly (Vector2 Center, Vector2 Radius)[] Ponds =
    [
        (new Vector2(146, 242), new Vector2(2.2f, 1.4f)),
        (new Vector2(137, 263), new Vector2(1.6f, 1.1f))
    ];

    private Rect2 _gridBounds;
    private Vector2 _canvasOrigin;

    public void Initialize(Vector2I coordinate)
    {
        _gridBounds = CountyCoordinateSpace.ChunkGridBounds(coordinate);
        _canvasOrigin = IsometricGrid.GridToScreen(_gridBounds.Position);
        // Parented to the landscape chunk, which already carries the offset.
        Position = Vector2.Zero;
        ZAsRelative = false;
        ZIndex = -86;
    }

    public override void _Ready() => QueueRedraw();

    public override void _Draw()
    {
        DrawEdge(LakeOutline, 5.5f, 78f, 1.1f);
        DrawEdge(CountyMacroLayout.BlackwaterRiver, 7f, 54f, .8f);
        DrawEdge(CountyTerrain.MillCreek, 6f, 46f, .7f);
        foreach ((Vector2 center, Vector2 radius) in Ponds)
            DrawEdge(Outline(center, radius), 1.3f, 44f, .55f);
    }

    /// <summary>
    /// Walk a waterline and dress both banks. Offsets and choices are hashed
    /// from the sample index, so the same shore is always dressed the same way.
    /// </summary>
    private void DrawEdge(Vector2[] line, float spacing, float height, float inset)
    {
        int sample = 0;
        for (int index = 0; index < line.Length - 1; index++)
        {
            Vector2 start = line[index];
            Vector2 end = line[index + 1];
            float length = start.DistanceTo(end);
            if (length <= .0001f)
                continue;
            Vector2 tangent = (end - start) / length;
            Vector2 normal = new(-tangent.Y, tangent.X);
            int steps = Mathf.Max(1, Mathf.CeilToInt(length / spacing));

            for (int step = 0; step < steps; step++, sample++)
            {
                Vector2 point = start.Lerp(end, (step + .5f) / steps);
                for (int side = -1; side <= 1; side += 2)
                {
                    float roll = CountyTerrain.Hash01(sample, side, 1307);
                    if (roll > .70f)
                        continue;
                    Vector2 at = point + normal * side * (inset + CountyTerrain.Hash01(sample, side, 1301) * 1.4f);
                    if (!_gridBounds.HasPoint(at))
                        continue;

                    string texture = roll switch
                    {
                        < .24f => WaterProps + "reeds_tall.png",
                        < .44f => WaterProps + "reeds_short.png",
                        < .60f => WaterProps + "shore_rocks.png",
                        _ => WaterProps + "lily_pads.png"
                    };
                    DrawProp(texture, at, height * (.8f + CountyTerrain.Hash01(sample, side, 1311) * .5f));
                }
            }
        }
    }

    private static Vector2[] Outline(Vector2 center, Vector2 radius)
    {
        Vector2[] points = new Vector2[19];
        for (int index = 0; index < points.Length; index++)
        {
            float angle = Mathf.Tau * index / (points.Length - 1);
            points[index] = center + new Vector2(Mathf.Cos(angle) * radius.X, Mathf.Sin(angle) * radius.Y);
        }
        return points;
    }

    private void DrawProp(string path, Vector2 point, float canvasHeight)
    {
        Texture2D texture = TextureRegistry.Get(path);
        if (texture is null)
            return;
        Vector2 source = texture.GetSize();
        if (source.Y <= 1f)
            return;
        Vector2 size = source * (canvasHeight / source.Y);
        Vector2 at = IsometricGrid.GridToScreen(point) - _canvasOrigin;
        DrawTextureRect(texture, new Rect2(at - new Vector2(size.X * .5f, size.Y), size), false, new Color(1, 1, 1, .93f));
    }
}
