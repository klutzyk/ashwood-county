#nullable enable

using System;

namespace AshwoodCounty.World.County.Visual;

/// <summary>
/// What a route is for, which decides how wide it reads and which authored kit
/// surfaces it.
///
/// This is derived from the existing logical road definition rather than stored
/// beside it, so there is exactly one road network. Rendering, width, verge
/// wear and junction behaviour all ask this classifier instead of guessing from
/// whichever texture happened to be picked.
/// </summary>
public enum CountyRoadClass
{
    /// <summary>Highway 16. Asphalt, engineered, handled by the ribbon renderer.</summary>
    Highway,

    /// <summary>Paved town streets.</summary>
    TownStreet,

    /// <summary>A proper county dirt road between regions. Trucks used these.</summary>
    MainRoad,

    /// <summary>A rural connector: narrower, less maintained, still a vehicle road.</summary>
    RuralRoad,

    /// <summary>Farm, cabin and property access. A lane rather than a road.</summary>
    AccessRoad,

    /// <summary>Worked woodland: rutted, muddy, grass down the middle.</summary>
    LoggingTrack,

    /// <summary>Foot only. Winds, narrow, may end at a trailhead.</summary>
    Trail
}

/// <summary>How a road class is drawn.</summary>
/// <param name="WidthCells">Ground-space width the authored art is fitted to.</param>
/// <param name="Straights">Kit pieces used along a run, in preference order.</param>
/// <param name="Curve">Kit piece used at a change of direction.</param>
/// <param name="JunctionThree">Kit piece for a three-armed meeting.</param>
/// <param name="JunctionFour">Kit piece for a four-armed meeting.</param>
/// <param name="VergeReach">
/// How far past the carriageway the ground reads as worn, in cells. This used
/// to be a flat 2.6 for every route, which painted a band of bare earth several
/// times wider than the road art sitting on it; the road then looked like a
/// sprite dropped onto a dirt platform.
/// </param>
public readonly record struct RoadClassProfile(
    float WidthCells,
    string[] Straights,
    string Curve,
    string JunctionThree,
    string JunctionFour,
    float VergeReach);

public static class CountyRoadClasses
{
    /// <summary>Classify a logical route. Ids are the county's own vocabulary.</summary>
    public static CountyRoadClass ClassOf(CountyRoadDefinition road)
    {
        if (road.Major)
            return CountyRoadClass.Highway;
        if (road.Id.StartsWith("ashwood_", StringComparison.Ordinal))
            return CountyRoadClass.TownStreet;
        if (road.Id.Contains("trail", StringComparison.Ordinal)
            || road.Id.Contains("path", StringComparison.Ordinal)
            || road.HalfWidth < .42f)
            return CountyRoadClass.Trail;
        if (road.Id.Contains("logging", StringComparison.Ordinal)
            || road.Id.Contains("lookout", StringComparison.Ordinal))
            return CountyRoadClass.LoggingTrack;
        if (road.Id.Contains("track", StringComparison.Ordinal)
            || road.Id.Contains("spur", StringComparison.Ordinal)
            || road.Id.Contains("drive", StringComparison.Ordinal)
            || road.HalfWidth < .70f)
            return CountyRoadClass.AccessRoad;
        if (road.HalfWidth < 1.15f)
            return CountyRoadClass.RuralRoad;
        return CountyRoadClass.MainRoad;
    }

    public static RoadClassProfile Profile(CountyRoadClass roadClass) => roadClass switch
    {
        // A county road a truck used. Widest of the dirt classes.
        CountyRoadClass.MainRoad => new(2.7f,
            ["dirt_straight"], "dirt_quarter_curve", "dirt_t_junction", "dirt_crossroad", 1.5f),

        CountyRoadClass.RuralRoad => new(2.2f,
            ["dirt_straight", "two_track_road"], "dirt_quarter_curve", "dirt_t_junction", "dirt_crossroad", 1.2f),

        // Farm lanes and driveways: same visual language, narrower ground width.
        CountyRoadClass.AccessRoad => new(1.75f,
            ["farm_track_straight", "two_track_road"], "dirt_quarter_curve", "dirt_y_junction", "dirt_t_junction", 1.0f),

        // Worked woodland. Rutted and muddy, and it keeps grass down the centre.
        CountyRoadClass.LoggingTrack => new(1.5f,
            ["logging_road_straight", "muddy_logging_road", "two_track_road"], "dirt_quarter_curve",
            "dirt_y_junction", "dirt_t_junction", .8f),

        // Foot only.
        CountyRoadClass.Trail => new(.95f,
            ["footpath_winding"], "", "", "", .55f),

        // Paved classes keep the ribbon renderer; the width here is only used
        // for verge wear.
        CountyRoadClass.Highway => new(3.2f, [], "", "", "", 2.2f),
        _ => new(2.0f, [], "", "", "", 1.4f)
    };

    public static RoadClassProfile ProfileOf(CountyRoadDefinition road) => Profile(ClassOf(road));

    /// <summary>True for routes drawn from the authored dirt kit.</summary>
    public static bool UsesDirtKit(CountyRoadDefinition road) =>
        ClassOf(road) is not (CountyRoadClass.Highway or CountyRoadClass.TownStreet);
}
