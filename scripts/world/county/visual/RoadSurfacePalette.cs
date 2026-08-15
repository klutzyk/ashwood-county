#nullable enable

using Godot;

namespace AshwoodCounty.World.County.Visual;

/// <summary>
/// How each class of Ashwood road is surfaced, using the project's seamless
/// road material textures.
///
/// The V band selects the part of a material worth using: those textures carry
/// their own grass verges, and the ground layer already paints a proper verge,
/// so only the carriageway strip is taken.
///
/// Stretch multiplies the aspect-correct repeat length. It must stay near 1.
/// Values around 2 were used to soften the dirt materials' baked meander, but
/// stretching a square texture four times along its length smears every stone
/// and rut into a continuous streak, which is exactly what made dirt roads read
/// as bundles of parallel brown lines. Softening belongs in the tint, not in
/// the UVs.
///
/// The classes below form a deliberate hierarchy. A highway is the strongest,
/// cleanest route on screen; a county road is clearly legible but sits in the
/// landscape; farm tracks and trails recede. Width, contrast, shoulder size and
/// wear frequency all step down together, because changing only one of them
/// leaves every road looking equally important.
/// </summary>
public readonly record struct RoadSurfaceProfile(
    string Surface,
    float SurfaceVLow,
    float SurfaceVHigh,
    float SurfaceStretch,
    Color SurfaceTint,
    string Shoulder,
    float ShoulderVLow,
    float ShoulderVHigh,
    float ShoulderStretch,
    Color ShoulderTint,
    float ShoulderWidth,
    bool CentreLine,
    string WearTexture,
    float WearSpacing);

public static class RoadSurfacePalette
{
    private const string Materials = "res://assets/art/roads/materials/";
    private const string RoadTiles = "res://assets/art/terrain/roads/";

    /// <summary>
    /// Roads are authored with a canvas-flavoured half width. This converts it
    /// to grid cells so ribbons project with correct isometric foreshortening
    /// while keeping the widths the county was laid out with.
    /// </summary>
    public const float GridWidthScale = .55f;

    // Strongest: full contrast, wide shoulder, painted centre line.
    private static readonly RoadSurfaceProfile Highway = new(
        Materials + "asphalt_surface.png", 0f, 1f, 1.0f, new Color(.92f, .92f, .90f),
        Materials + "asphalt_shoulder.png", .20f, .78f, 1.0f, new Color(.74f, .72f, .66f),
        .80f, true, RoadTiles + "asphalt_wear_01.png", 17f);

    private static readonly RoadSurfaceProfile TownStreet = new(
        Materials + "asphalt_worn_surface.png", 0f, 1f, 1.0f, new Color(.86f, .86f, .84f),
        Materials + "asphalt_shoulder.png", .22f, .76f, 1.0f, new Color(.70f, .69f, .64f),
        .55f, false, RoadTiles + "asphalt_cracked_01.png", 21f);

    // Clearly readable, but sat down into the landscape: the tint is pulled
    // well below white so a dirt road stops out-shouting the ground it crosses.
    private static readonly RoadSurfaceProfile CountyRoad = new(
        Materials + "dirt_surface.png", .30f, .70f, 1.0f, new Color(.80f, .77f, .70f),
        Materials + "dirt_shoulder.png", .06f, .34f, 1.0f, new Color(.72f, .71f, .62f),
        .62f, false, RoadTiles + "gravel_road_01.png", 22f);

    // Quieter again, and narrower.
    private static readonly RoadSurfaceProfile FarmTrack = new(
        Materials + "farm_track_surface.png", .32f, .68f, 1.0f, new Color(.76f, .73f, .66f),
        Materials + "dirt_shoulder.png", .08f, .34f, 1.0f, new Color(.70f, .69f, .60f),
        .46f, false, RoadTiles + "dirt_ruts_02.png", 28f);

    private static readonly RoadSurfaceProfile ForestTrack = new(
        Materials + "two_track_surface.png", .30f, .70f, 1.0f, new Color(.72f, .73f, .65f),
        Materials + "mud_surface.png", .34f, .66f, 1.0f, new Color(.66f, .65f, .57f),
        .44f, false, RoadTiles + "forest_track_02.png", 26f);

    // Quietest: a worn line through the grass, no more.
    private static readonly RoadSurfaceProfile Footpath = new(
        Materials + "footpath_surface.png", .38f, .62f, 1.0f, new Color(.78f, .75f, .68f),
        Materials + "dirt_shoulder.png", .12f, .36f, 1.0f, new Color(.72f, .71f, .64f),
        .38f, false, RoadTiles + "rural_path_grass_01.png", 30f);

    public static RoadSurfaceProfile For(CountyRoadDefinition road)
    {
        if (road.Major)
            return Highway;
        if (road.Id.StartsWith("ashwood_", System.StringComparison.Ordinal))
            return TownStreet;
        if (road.Id.Contains("logging", System.StringComparison.Ordinal)
            || road.Id.Contains("ridge", System.StringComparison.Ordinal)
            || road.Id.Contains("lookout", System.StringComparison.Ordinal))
            return ForestTrack;
        if (road.HalfWidth < .42f)
            return Footpath;
        if (road.HalfWidth < .70f || road.Id.Contains("farm", System.StringComparison.Ordinal))
            return FarmTrack;
        return CountyRoad;
    }

    /// <summary>Every material the palette can request, for start-up warm-up.</summary>
    public static readonly string[] AllTextures =
    [
        Materials + "asphalt_surface.png",
        Materials + "asphalt_worn_surface.png",
        Materials + "asphalt_shoulder.png",
        Materials + "dirt_surface.png",
        Materials + "dirt_shoulder.png",
        Materials + "farm_track_surface.png",
        Materials + "two_track_surface.png",
        Materials + "mud_surface.png",
        Materials + "footpath_surface.png"
    ];
}
