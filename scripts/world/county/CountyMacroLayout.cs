#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

namespace AshwoodCounty.World.County;

public enum CountyContentTier
{
    FullDetail,
    MacroPlayable,
    LandscapeFoundation
}

public enum CountyLocationKind
{
    District,
    Landmark
}

public sealed record CountyLocationDefinition(
    string Id,
    string Name,
    CountyLocationKind Kind,
    Vector2 Center,
    Vector2 Radius,
    CountyContentTier ContentTier,
    string ParentRegionId = "")
{
    public Rect2 Bounds => new(Center - Radius, Radius * 2f);
}

public sealed record CountyRoadDefinition(
    string Id,
    string Name,
    float HalfWidth,
    Vector2[] Points,
    bool Major = false);

public sealed record CountyLandUseDefinition(
    string Id,
    Vector2 Center,
    Vector2 Radius,
    Color Color);

/// <summary>
/// Canonical gameplay-space blockout. These are authored county coordinates,
/// not pixels copied from the concept map. X increases east and Y south.
/// </summary>
public static class CountyMacroLayout
{
    public const string WildernessRegionId = "county_wilderness";
    public static readonly Vector2 FarmDistrictEntry = new(180, 190);
    public static readonly Vector2 MillCreekEntry = new(160, 232);

    public static readonly CountyLocationDefinition[] Locations =
    [
        D("pine_ridge", "Pine Ridge", 72, 37, 61, 32, CountyContentTier.LandscapeFoundation),
        D("logging_camp", "Logging Camp", 105, 74, 27, 21, CountyContentTier.MacroPlayable),
        D("fire_lookout", "Fire Lookout", 311, 54, 22, 18, CountyContentTier.LandscapeFoundation),
        D("blackwater_lake", "Blackwater Lake", 246, 70, 70, 42, CountyContentTier.LandscapeFoundation),
        D("blackwater_dam", "Blackwater Dam", 301, 103, 23, 18, CountyContentTier.MacroPlayable),
        D("old_mill_bridge", "Old Mill Bridge", 166, 121, 24, 17, CountyContentTier.MacroPlayable),
        D("farm_district", "Farm District", 170, 204, 44, 37, CountyContentTier.FullDetail),
        D("outskirts", "Ashwood Outskirts", 197, 157, 34, 30, CountyContentTier.FullDetail),
        D("ashwood", "Ashwood", 252, 145, 48, 42, CountyContentTier.MacroPlayable),
        D("service_station", "Service Station", 226, 190, 18, 15, CountyContentTier.MacroPlayable),
        D("trailer_park", "Trailer Park", 279, 211, 29, 23, CountyContentTier.MacroPlayable),
        D("county_fairgrounds", "County Fairgrounds", 246, 234, 38, 27, CountyContentTier.MacroPlayable),
        D("south_farmland", "South Farmland", 164, 254, 70, 53, CountyContentTier.MacroPlayable),
        D("mill_creek", "Mill Creek", 154, 250, 39, 35, CountyContentTier.FullDetail),

        L("hospital", "Ashwood County Hospital", 244, 151, 8, 7, "ashwood"),
        L("sheriffs_office", "Sheriff's Office", 272, 137, 7, 6, "ashwood"),
        L("old_mill", "Old Mill", 154, 248, 9, 8, "mill_creek"),
        L("farm_silos", "Farm District Grain Silos", 168, 201, 7, 6, "farm_district"),
        L("starting_camp", "Starting Camp", 203, 157, 7, 6, "outskirts"),
        L("dam_control", "Blackwater Dam Control House", 301, 103, 8, 6, "blackwater_dam"),
        L("lookout_tower", "Fire Lookout Tower", 311, 54, 5, 5, "fire_lookout")
    ];

    public static IReadOnlyList<CountyLocationDefinition> Regions { get; } =
        Locations.Where(location => location.Kind == CountyLocationKind.District).ToArray();

    public static readonly CountyLandUseDefinition[] LandUses =
    [
        new("north_forest", new Vector2(148, 54), new Vector2(151, 61), new Color("#364b33")),
        new("pine_highlands", new Vector2(66, 34), new Vector2(70, 37), new Color("#2e4234")),
        new("western_fields", new Vector2(170, 204), new Vector2(55, 46), new Color("#778150")),
        new("outskirts_woodland", new Vector2(197, 157), new Vector2(45, 37), new Color("#5d7448")),
        new("mill_woods", new Vector2(154, 250), new Vector2(47, 41), new Color("#3f654d")),
        new("south_fields", new Vector2(164, 257), new Vector2(90, 55), new Color("#808552")),
        new("ashwood_urban", new Vector2(253, 146), new Vector2(51, 40), new Color("#62645a")),
        new("eastern_scrub", new Vector2(315, 180), new Vector2(66, 92), new Color("#536746")),
        new("fairgrounds", new Vector2(246, 234), new Vector2(40, 29), new Color("#727453"))
    ];

    public static readonly CountyRoadDefinition[] Roads =
    [
        new("highway_16", "Highway 16", 2.25f,
        [new(18, 151), new(70, 150), new(116, 153), new(153, 150), new(192, 146), new(229, 144), new(264, 143), new(307, 137), new(364, 130)], true),

        // The first real traversal spine: camp -> Farm District -> Mill Creek.
        new("farm_mill_road", "Farm and Mill Road", 1.45f,
        [new(206, 157), new(199, 166), new(192, 176), new(180, 190), new(170, 204), new(166, 218), new(160, 232), new(154, 250), new(151, 262)]),

        new("south_county_road", "South County Road", 1.35f,
        [new(142, 169), new(151, 202), new(164, 232), new(190, 245), new(224, 240), new(247, 233), new(277, 211)]),
        new("ashwood_south_approach", "Ashwood South Approach", 1.5f,
        [new(229, 144), new(227, 166), new(226, 190), new(247, 202), new(277, 211)]),
        new("old_mill_road", "Old Mill Road", 1.2f,
        [new(157, 150), new(164, 135), new(166, 121), new(145, 104), new(121, 86), new(105, 74)]),
        new("ridge_road", "Pine Ridge Road", 1.05f,
        [new(105, 74), new(89, 56), new(72, 37), new(112, 42), new(158, 51), new(202, 61)]),
        new("lake_road", "Blackwater Lake Road", 1.15f,
        [new(202, 61), new(237, 48), new(273, 55), new(301, 79), new(301, 103)]),
        new("dam_road", "Dam Road", 1.2f,
        [new(301, 103), new(306, 120), new(307, 137)]),
        new("lookout_track", "Fire Lookout Track", .85f,
        [new(273, 55), new(292, 48), new(311, 54)])
    ];

    public static readonly Vector2[] BlackwaterLake =
    [
        new(190, 52), new(211, 35), new(245, 30), new(278, 37),
        new(302, 56), new(296, 78), new(274, 90), new(239, 88),
        new(208, 78), new(186, 66)
    ];

    /// <summary>
    /// Blackwater Lake's rendered shoreline: the authored polygon subdivided and
    /// perturbed deterministically. The blockout's ten points make a shape that
    /// reads as a cut-out from the air; a real lake edge bays and juts. Water
    /// rendering, shore dressing and ground surfacing all use this one outline
    /// so they cannot disagree about where the waterline is.
    /// </summary>
    public static readonly Vector2[] BlackwaterLakeOutline = BuildLakeOutline();

    private static Vector2[] BuildLakeOutline()
    {
        const int subdivisions = 7;
        List<Vector2> outline = [];
        for (int index = 0; index < BlackwaterLake.Length; index++)
        {
            Vector2 a = BlackwaterLake[index];
            Vector2 b = BlackwaterLake[(index + 1) % BlackwaterLake.Length];
            Vector2 tangent = (b - a).Normalized();
            Vector2 normal = new(-tangent.Y, tangent.X);
            for (int step = 0; step < subdivisions; step++)
            {
                float t = step / (float)subdivisions;
                Vector2 point = a.Lerp(b, t);
                // Two scales of wobble: broad bays plus a smaller ragged edge.
                float wobble =
                    Mathf.Sin((index * subdivisions + step) * .93f) * 2.3f +
                    Mathf.Sin((index * subdivisions + step) * 2.61f + 1.7f) * 1.1f;
                outline.Add(point + normal * wobble);
            }
        }
        return [.. outline];
    }

    public static readonly Vector2[] BlackwaterRiver =
    [new(286, 79), new(296, 101), new(282, 124), new(257, 146), new(224, 171), new(187, 193), new(149, 207), new(111, 220), new(82, 245), new(63, 287)];

    /// <summary>
    /// The river as drawn: the blockout course subdivided and given a gentle
    /// meander. Straight twenty-cell runs read as a canal, not a river. Water
    /// rendering, bank dressing and ground surfacing all use this one course so
    /// they cannot disagree about where the water is.
    /// </summary>
    public static readonly Vector2[] BlackwaterRiverCourse = Meander(BlackwaterRiver, 6, 2.1f, 0.77f);

    /// <summary>
    /// Subdivide a polyline and push each new point sideways on a smooth,
    /// deterministic wave. Endpoints stay put so connections are preserved.
    /// </summary>
    public static Vector2[] Meander(Vector2[] line, int subdivisions, float amplitude, float wavelength)
    {
        List<Vector2> course = [];
        int sample = 0;
        for (int index = 0; index < line.Length - 1; index++)
        {
            Vector2 a = line[index];
            Vector2 b = line[index + 1];
            Vector2 tangent = (b - a).Normalized();
            Vector2 normal = new(-tangent.Y, tangent.X);
            for (int step = 0; step < subdivisions; step++, sample++)
            {
                float t = step / (float)subdivisions;
                bool anchor = index == 0 && step == 0;
                float wobble = anchor ? 0f
                    : Mathf.Sin(sample * wavelength) * amplitude
                      + Mathf.Sin(sample * wavelength * 2.7f + 1.3f) * amplitude * .38f;
                course.Add(a.Lerp(b, t) + normal * wobble);
            }
        }
        course.Add(line[^1]);
        return [.. course];
    }

    public static CountyLocationDefinition? Find(string id) =>
        Locations.FirstOrDefault(location => location.Id == id);

    public static CountyRoadDefinition? FindRoad(string id) =>
        Roads.FirstOrDefault(road => road.Id == id);

    public static CountyLocationDefinition RegionAt(Vector2 gridPosition)
    {
        CountyLocationDefinition? containing = Locations
            .Where(location => location.Kind == CountyLocationKind.District && Contains(location, gridPosition))
            .OrderBy(location => NormalizedDistanceSquared(location, gridPosition))
            .FirstOrDefault();

        return containing ?? new CountyLocationDefinition(
            WildernessRegionId,
            "County Wilderness",
            CountyLocationKind.District,
            CountyCoordinateSpace.GridBounds.GetCenter(),
            CountyCoordinateSpace.GridBounds.Size * .5f,
            CountyContentTier.LandscapeFoundation);
    }

    public static bool Contains(CountyLocationDefinition location, Vector2 gridPosition) =>
        NormalizedDistanceSquared(location, gridPosition) <= 1f;

    private static float NormalizedDistanceSquared(CountyLocationDefinition location, Vector2 point)
    {
        Vector2 offset = point - location.Center;
        float x = offset.X / Mathf.Max(1, location.Radius.X);
        float y = offset.Y / Mathf.Max(1, location.Radius.Y);
        return x * x + y * y;
    }

    private static CountyLocationDefinition D(string id, string name, float x, float y, float rx, float ry, CountyContentTier tier) =>
        new(id, name, CountyLocationKind.District, new Vector2(x, y), new Vector2(rx, ry), tier);

    private static CountyLocationDefinition L(string id, string name, float x, float y, float rx, float ry, string parent) =>
        new(id, name, CountyLocationKind.Landmark, new Vector2(x, y), new Vector2(rx, ry), CountyContentTier.MacroPlayable, parent);
}
