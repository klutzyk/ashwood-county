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

/// <summary>How a composed placement is drawn.</summary>
public enum RoadPieceKind
{
    /// <summary>A cross-section slice of a straight, fitted to grid cells.</summary>
    Slice,

    /// <summary>A whole piece drawn untransformed in screen space.</summary>
    Sprite
}

/// <summary>
/// One composed road placement, resolved to final geometry.
///
/// Composition is cached, so the arrays here are built once for the county and
/// then only read while drawing.
/// </summary>
public sealed record RoadPiecePlacement(
    RoadPieceKind Kind,
    string Texture,
    Vector2 Anchor,
    Vector2[] GridCorners,
    Vector2[] Uvs,
    float SpriteScale,
    Color Tint);

/// <summary>
/// The authored isometric dirt-road construction kit.
///
/// The pieces are pre-rendered isometric artwork. Perspective, verge shape and
/// lighting are baked in, so a piece is only valid in the orientation it was
/// drawn in, plus a horizontal mirror; a mirror is safe because reflecting the
/// screen about its vertical axis maps one ground axis onto the other and
/// leaves the horizon alone.
///
/// The important change over the first version of this kit is that a straight
/// is no longer placed as a whole sprite. Whole sprites have worn, ragged ends,
/// so butting two of them together showed grass through the notch and the road
/// read as a row of separate slabs. Instead an arbitrary window along the
/// middle of the piece is taken as a cross-section slice, and slices are laid
/// edge to edge. A cross-section has a clean straight cut across the road, so
/// consecutive slices share an edge exactly and there is nothing to see through.
/// The ragged part of the artwork that still matters, the verge along the road's
/// sides, is untouched.
/// </summary>
public static class DirtRoadKit
{
    private const string Reference = "res://assets/art/roads/dirt/reference/";

    /// <summary>
    /// A piece's authored parallelogram, as the four extreme points of its
    /// opaque area in source pixels, ordered so the first edge runs along the
    /// road and the second across it.
    /// </summary>
    private readonly record struct Shape(string Path, Vector2 Left, Vector2 Top, Vector2 Right, Vector2 Bottom);

    // Measured from the artwork, not assumed from filenames.
    private static readonly Dictionary<string, Shape> Shapes = new()
    {
        ["dirt_straight"] = new(Reference + "dirt_straight.png",
            new(3, 180), new(266, 3), new(400, 82), new(128, 278)),
        ["farm_track_straight"] = new(Reference + "farm_track_straight.png",
            new(3, 180), new(270, 3), new(395, 79), new(127, 269)),
        ["logging_road_straight"] = new(Reference + "logging_road_straight.png",
            new(4, 200), new(302, 4), new(422, 76), new(133, 292)),
        ["two_track_road"] = new(Reference + "two_track_road.png",
            new(3, 248), new(225, 4), new(342, 33), new(183, 326)),
        ["muddy_logging_road"] = new(Reference + "muddy_logging_road.png",
            new(4, 210), new(280, 4), new(393, 92), new(130, 337)),
        ["footpath_winding"] = new(Reference + "footpath_winding.png",
            new(4, 250), new(200, 4), new(284, 60), new(120, 361)),
    };

    /// <summary>Pieces drawn untransformed: their arms already point along the ground axes.</summary>
    private static readonly Dictionary<string, string> SpritePieces = new()
    {
        ["dirt_crossroad"] = Reference + "dirt_crossroad.png",
        ["dirt_t_junction"] = Reference + "dirt_t_junction.png",
        ["dirt_y_junction"] = Reference + "dirt_y_junction.png",
        ["dirt_quarter_curve"] = Reference + "dirt_quarter_curve.png",
        ["dirt_turnaround"] = Reference + "dirt_turnaround.png",
    };

    /// <summary>
    /// How much road, in grid cells, a whole straight piece represents along its
    /// length. Used to keep texel density roughly constant when a slice takes
    /// only part of the source.
    /// </summary>
    private const float PieceAlongCells = 3.4f;

    public static bool HasStraight(string piece) => Shapes.ContainsKey(piece);

    public static bool HasSprite(string piece) => SpritePieces.ContainsKey(piece);

    public static string StraightPath(string piece) => Shapes[piece].Path;

    public static string SpritePath(string piece) => SpritePieces[piece];

    /// <summary>Every texture the kit can request, for start-up warm-up.</summary>
    public static IEnumerable<string> AllTextures()
    {
        foreach (Shape shape in Shapes.Values)
            yield return shape.Path;
        foreach (string path in SpritePieces.Values)
            yield return path;
    }

    /// <summary>
    /// Grid corners for a slice running from <paramref name="lo"/> to
    /// <paramref name="hi"/> along an axis.
    ///
    /// The centre line is allowed to drift between the two ends. A route that
    /// falls twenty-six cells sideways over forty cannot be drawn as runs pinned
    /// to one fixed coordinate without stepping sideways in visible jumps, which
    /// is what made a shallow diagonal read as a flight of separate slabs.
    /// Letting each slice lean slightly, while its edges stay parallel to the
    /// authored piece's own axes, follows the route without rotating the art.
    ///
    /// Both ends are exact and a slice takes its neighbour's offset at the
    /// shared boundary, so consecutive slices share an edge and no gap opens.
    /// </summary>
    public static Vector2[] SliceCorners(
        RoadAxis axis, float lo, float hi, float offsetLo, float offsetHi, float width)
    {
        float half = width * .5f;
        if (axis == RoadAxis.NorthEast)
        {
            // Runs along grid Y; the carriageway spreads along X.
            return
            [
                new Vector2(offsetHi - half, hi),
                new Vector2(offsetLo - half, lo),
                new Vector2(offsetLo + half, lo),
                new Vector2(offsetHi + half, hi)
            ];
        }

        return
        [
            new Vector2(lo, offsetLo - half),
            new Vector2(hi, offsetHi - half),
            new Vector2(hi, offsetHi + half),
            new Vector2(lo, offsetLo + half)
        ];
    }

    /// <summary>
    /// UVs for a window along a straight, from <paramref name="u0"/> to
    /// <paramref name="u1"/> of its authored length.
    ///
    /// Taking a different window for each slice is what stops a run repeating
    /// the same stones and ruts on a visible beat, without rotating anything.
    /// </summary>
    public static Vector2[] SliceUvs(string piece, float u0, float u1, bool mirror)
    {
        Shape shape = Shapes[piece];
        Texture2D texture = TextureRegistry.Get(shape.Path);
        Vector2 size = texture is null ? Vector2.One : texture.GetSize();

        Vector2 along = shape.Top - shape.Left;
        Vector2 across = shape.Right - shape.Top;
        Vector2[] corners =
        [
            shape.Left + along * u0,
            shape.Left + along * u1,
            shape.Left + along * u1 + across,
            shape.Left + along * u0 + across
        ];

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

    /// <summary>Fraction of a straight's source length that covers a given run length.</summary>
    public static float SourceSpanFor(float cells) => Mathf.Clamp(cells / PieceAlongCells, .12f, .92f);

    /// <summary>
    /// How large a sprite piece is drawn, so its arms match the road width it
    /// serves. The pieces are authored a little wider than this county's rural
    /// routes, so they are reduced rather than enlarged.
    /// </summary>
    public static float SpriteScaleFor(string piece, float roadWidthCells)
    {
        // Arm width as a fraction of the bitmap's width, measured from the art.
        float armFraction = piece switch
        {
            "dirt_crossroad" => .30f,
            "dirt_t_junction" => .31f,
            "dirt_y_junction" => .30f,
            "dirt_quarter_curve" => .40f,
            _ => .32f
        };
        Texture2D texture = TextureRegistry.Get(SpritePath(piece));
        if (texture is null)
            return .8f;

        // A road of w cells across projects to roughly w * 53.7px on screen.
        float wantedArmPixels = roadWidthCells * 53.7f;
        float nativeArmPixels = texture.GetSize().X * armFraction;
        return Mathf.Clamp(wantedArmPixels / Mathf.Max(1f, nativeArmPixels), .25f, 1f);
    }

    /// <summary>
    /// Radius, in cells, that straights must keep clear of around a sprite piece.
    ///
    /// This is deliberately well inside the sprite's own half width. A junction
    /// or curve bitmap is mostly transparent at its corners, so clearing its
    /// full extent takes away road the artwork never covers and opens a wedge of
    /// grass at exactly the place the road is supposed to bend. Keeping the
    /// clearance to the road-bearing core lets the straights run under the piece
    /// and the two overlap instead of leaving a hole.
    /// </summary>
    public static float SpriteCoverCells(string piece, float roadWidthCells)
    {
        Texture2D texture = TextureRegistry.Get(SpritePath(piece));
        if (texture is null)
            return roadWidthCells * .5f;
        float scale = SpriteScaleFor(piece, roadWidthCells);
        float halfWidthCells = texture.GetSize().X * scale * .5f / 53.7f;
        float core = piece == "dirt_quarter_curve" ? .42f : .62f;
        return halfWidthCells * core;
    }
}
