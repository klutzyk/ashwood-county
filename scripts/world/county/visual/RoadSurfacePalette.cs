#nullable enable

using Godot;

namespace AshwoodCounty.World.County.Visual;

/// <summary>
/// How each class of Ashwood road is surfaced, using the project's seamless
/// road material textures.
///
/// The V band selects the part of a material worth using: those textures carry
/// their own grass verges, and the ground layer already paints a proper verge,
/// so only the carriageway strip is taken. Stretch is a multiplier on the
/// aspect-correct repeat length, which is how the dirt materials' baked meander
/// becomes a long gentle wander instead of a tight repeating zig-zag.
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

    private static readonly RoadSurfaceProfile Highway = new(
        Materials + "asphalt_surface.png", 0f, 1f, 1.0f, new Color(.90f, .90f, .88f),
        Materials + "asphalt_shoulder.png", .20f, .78f, 1.2f, new Color(.72f, .70f, .64f),
        .78f, true, RoadTiles + "asphalt_wear_01.png", 17f);

    private static readonly RoadSurfaceProfile TownStreet = new(
        Materials + "asphalt_worn_surface.png", 0f, 1f, 1.0f, new Color(.86f, .86f, .84f),
        Materials + "asphalt_shoulder.png", .22f, .76f, 1.2f, new Color(.70f, .69f, .64f),
        .55f, false, RoadTiles + "asphalt_cracked_01.png", 21f);

    private static readonly RoadSurfaceProfile CountyRoad = new(
        Materials + "dirt_surface.png", .32f, .68f, 2.1f, new Color(.96f, .93f, .87f),
        Materials + "dirt_shoulder.png", .04f, .34f, 2.4f, new Color(.86f, .84f, .74f),
        .82f, false, RoadTiles + "gravel_road_01.png", 15f);

    private static readonly RoadSurfaceProfile FarmTrack = new(
        Materials + "farm_track_surface.png", .30f, .70f, 2.0f, new Color(.94f, .91f, .84f),
        Materials + "dirt_shoulder.png", .06f, .36f, 2.2f, new Color(.84f, .83f, .72f),
        .70f, false, RoadTiles + "dirt_ruts_02.png", 13f);

    private static readonly RoadSurfaceProfile ForestTrack = new(
        Materials + "two_track_surface.png", .28f, .72f, 2.0f, new Color(.88f, .89f, .80f),
        Materials + "mud_surface.png", .32f, .68f, 2.2f, new Color(.78f, .76f, .66f),
        .62f, false, RoadTiles + "forest_track_02.png", 12f);

    private static readonly RoadSurfaceProfile Footpath = new(
        Materials + "footpath_surface.png", .36f, .64f, 1.9f, new Color(.96f, .93f, .87f),
        Materials + "dirt_shoulder.png", .10f, .38f, 2.1f, new Color(.86f, .85f, .76f),
        .90f, false, RoadTiles + "rural_path_grass_01.png", 11f);

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
