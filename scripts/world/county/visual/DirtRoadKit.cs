#nullable enable

using System.Collections.Generic;
using Godot;

namespace AshwoodCounty.World.County.Visual;

/// <summary>Which way an authored road piece runs across the isometric grid.</summary>
public enum RoadAxis
{
    /// <summary>Up and to the right on screen: decreasing grid Y.</summary>
    NorthEast,

    /// <summary>Down and to the right on screen: increasing grid X.</summary>
    SouthEast
}

/// <summary>One authored road piece placed on the county grid.</summary>
public readonly record struct RoadPiecePlacement(
    string Texture, Vector2 Origin, RoadAxis Axis, float Along, float Across, bool Mirror, Color Tint);

/// <summary>
/// The authored isometric dirt-road construction kit.
///
/// The previous roads were a UV-mapped ribbon swept along the logical spline.
/// That is why they read as railway track: the spline bends constantly, so the
/// ribbon sheared the surface texture around every wobble, the two shoulder
/// bands ran perfectly parallel to the carriageway for its whole length, and
/// mitred corners produced hard polygon notches. No amount of better texture
/// fixes a road whose geometry is a mathematical line.
///
/// These pieces are pre-rendered isometric artwork instead. Perspective,
/// verge shape and lighting are baked in, so a piece is only valid in the
/// orientation it was drawn in, plus a horizontal mirror. A horizontal mirror
/// is safe because reflecting the screen about its vertical axis maps one
/// isometric ground axis onto the other and leaves the horizon unchanged;
/// rotating them, which is what a spline sweep effectively does, is not.
///
/// Each piece is therefore drawn as an affine quad: its authored parallelogram
/// is mapped onto the parallelogram of grid cells it should occupy. That fits
/// the art to this project's 2:1 grid exactly, keeps neighbouring pieces
/// sharing an edge, and never stretches the surface along its length.
/// </summary>
public static class DirtRoadKit
{
    private const string Reference = "res://assets/art/roads/dirt/reference/";

    /// <summary>
    /// A piece's authored parallelogram, as the four extreme points of its
    /// opaque area in source pixels, ordered so that the first edge runs along
    /// the road and the second across it.
    /// </summary>
    private readonly record struct Shape(
        string Path, Vector2 Left, Vector2 Top, Vector2 Right, Vector2 Bottom, float Along, float Across);

    // Measured from the artwork rather than assumed: these are the corners of
    // each piece's opaque parallelogram. Along/Across are the grid footprints
    // that make the carriageway about a cell and a half wide, which matches the
    // logical widths the county was laid out with.
    private static readonly Dictionary<string, Shape> Shapes = new()
    {
        ["dirt_straight"] = new(Reference + "dirt_straight.png",
            new(3, 180), new(266, 3), new(400, 82), new(128, 278), 3.4f, 2.0f),
        ["farm_track_straight"] = new(Reference + "farm_track_straight.png",
            new(3, 180), new(270, 3), new(395, 79), new(127, 269), 3.4f, 1.9f),
        ["logging_road_straight"] = new(Reference + "logging_road_straight.png",
            new(4, 200), new(302, 4), new(422, 76), new(133, 292), 3.6f, 2.0f),
        ["two_track_road"] = new(Reference + "two_track_road.png",
            new(3, 248), new(225, 4), new(342, 33), new(183, 326), 3.2f, 1.7f),
        ["muddy_logging_road"] = new(Reference + "muddy_logging_road.png",
            new(4, 210), new(280, 4), new(393, 92), new(130, 337), 3.4f, 2.0f),
        // Junction and curve pieces. Their opaque area is not a simple
        // parallelogram, so the footprint below is the square of grid cells the
        // piece should cover and the corners are the bounding box: the artwork's
        // own transparency does the shaping.
        ["dirt_crossroad"] = new(Reference + "dirt_crossroad.png",
            new(4, 184), new(108, 4), new(394, 184), new(92, 257), 4.6f, 4.6f),
        ["dirt_t_junction"] = new(Reference + "dirt_t_junction.png",
            new(4, 156), new(251, 6), new(391, 191), new(300, 261), 4.4f, 4.4f),
        ["dirt_y_junction"] = new(Reference + "dirt_y_junction.png",
            new(4, 150), new(150, 4), new(357, 150), new(180, 252), 4.2f, 4.2f),
        ["dirt_quarter_curve"] = new(Reference + "dirt_quarter_curve.png",
            new(5, 203), new(278, 4), new(283, 64), new(128, 274), 4.0f, 4.0f),
        ["footpath_winding"] = new(Reference + "footpath_winding.png",
            new(4, 250), new(200, 4), new(284, 60), new(120, 361), 3.2f, 1.7f),
    };

    /// <summary>Straight surfacing for each class of dirt route.</summary>
    public static string StraightFor(CountyRoadDefinition road)
    {
        if (road.Id.Contains("logging", System.StringComparison.Ordinal))
            return "muddy_logging_road";
        if (road.Id.Contains("ridge", System.StringComparison.Ordinal)
            || road.Id.Contains("lookout", System.StringComparison.Ordinal)
            || road.Id.Contains("mill", System.StringComparison.Ordinal))
            return "logging_road_straight";
        if (road.HalfWidth < .42f)
            return "footpath_winding";
        if (road.Id.Contains("farm", System.StringComparison.Ordinal) || road.HalfWidth < .70f)
            return "farm_track_straight";
        return road.HalfWidth < 1.0f ? "two_track_road" : "dirt_straight";
    }

    public static bool Has(string piece) => Shapes.ContainsKey(piece);

    /// <summary>Every texture the kit can request, for start-up warm-up.</summary>
    public static IEnumerable<string> AllTextures()
    {
        foreach (Shape shape in Shapes.Values)
            yield return shape.Path;
    }

    public static string TexturePath(string piece) => Shapes[piece].Path;

    public static float AlongCells(string piece) => Shapes[piece].Along;

    public static float AcrossCells(string piece) => Shapes[piece].Across;

    /// <summary>
    /// The grid-space corners a piece covers, in the same order as its source
    /// corners, so the two can be handed straight to a textured quad.
    ///
    /// The road runs along the first axis and the verges sit either side of it,
    /// which is why the origin is offset by half the across width: a route's
    /// centre line should run down the middle of the carriageway, not along its
    /// edge.
    /// </summary>
    public static Vector2[] GridCorners(Vector2 origin, RoadAxis axis, float along, float across)
    {
        if (axis == RoadAxis.NorthEast)
        {
            // Road runs towards decreasing Y; verges spread along X.
            Vector2 a = origin - new Vector2(across * .5f, 0);
            return
            [
                a,
                a - new Vector2(0, along),
                a + new Vector2(across, -along),
                a + new Vector2(across, 0)
            ];
        }

        // Road runs towards increasing X; verges spread along Y.
        Vector2 b = origin - new Vector2(0, across * .5f);
        return
        [
            b,
            b + new Vector2(along, 0),
            b + new Vector2(along, across),
            b + new Vector2(0, across)
        ];
    }

    /// <summary>Source-pixel corners as UVs, mirrored horizontally when asked.</summary>
    /// <summary>
    /// Junction and curve pieces cover a square block of cells centred on the
    /// meeting point, rather than a length of road, because their artwork
    /// contains the road arms in every direction they serve.
    /// </summary>
    public static Vector2[] BlockCorners(Vector2 centre, float cells)
    {
        float half = cells * .5f;
        return
        [
            centre + new Vector2(-half, -half),
            centre + new Vector2(half, -half),
            centre + new Vector2(half, half),
            centre + new Vector2(-half, half)
        ];
    }

    /// <summary>
    /// How large a junction sprite is drawn, as a fraction of native.
    ///
    /// Chosen so the width of the piece's arms matches the composed road width;
    /// the pieces are authored a little wider than this project's rural routes.
    /// </summary>
    public static float JunctionScale(string piece) => piece switch
    {
        "dirt_crossroad" => .82f,
        "dirt_t_junction" => .82f,
        "dirt_y_junction" => .80f,
        _ => .80f
    };

    /// <summary>Whole-bitmap UVs, for pieces shaped by their own alpha.</summary>
    public static Vector2[] BlockUvs(bool mirror) => mirror
        ? [new(1, 0), new(0, 0), new(0, 1), new(1, 1)]
        : [new(0, 0), new(1, 0), new(1, 1), new(0, 1)];

    public static Vector2[] SourceUvs(string piece, bool mirror)
    {
        Shape shape = Shapes[piece];
        Texture2D texture = TextureRegistry.Get(shape.Path);
        Vector2 size = texture is null ? Vector2.One : texture.GetSize();
        Vector2[] corners = [shape.Left, shape.Top, shape.Right, shape.Bottom];
        Vector2[] uvs = new Vector2[4];
        for (int index = 0; index < 4; index++)
        {
            Vector2 point = corners[index];
            if (mirror)
                point = new Vector2(size.X - point.X, point.Y);
            uvs[index] = point / size;
        }
        return uvs;
    }
}
