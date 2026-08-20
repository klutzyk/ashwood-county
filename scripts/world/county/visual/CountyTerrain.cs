#nullable enable

using System;
using System.Collections.Generic;
using Godot;

namespace AshwoodCounty.World.County.Visual;

/// <summary>
/// Regional identity of a county point. Used for both ground surfacing and
/// vegetation composition so the two always agree.
/// </summary>
public enum CountyBiome
{
    Meadow,
    Forest,
    PineRidge,
    Logging,
    Outskirts,
    Farm,
    Mill,
    SouthFarm,
    Urban,
    TrailerPark,
    Fairgrounds,
    Scrub,
    Water
}

/// <summary>What a cultivated field is doing this season.</summary>
public enum FieldState
{
    Ploughed,
    Sown,
    Standing,
    Fallow
}

/// <summary>
/// The painted ground material at a county point. These map onto the authored
/// isometric ground diamonds in <see cref="GroundTilePalette"/>.
/// </summary>
public enum GroundSurface
{
    None,
    Meadow,
    RichMeadow,
    Pasture,
    DryGrass,
    ForestFloor,
    PineFloor,
    Scrub,
    Farmland,
    Ploughed,
    BareEarth,
    Gravel,
    Mud,
    Wetland,
    TownGround,
    Trodden
}

/// <summary>
/// One shared, deterministic description of Ashwood County's surface. Ground
/// tiling, vegetation scatter and road dressing all read from here, so the
/// landscape stays internally consistent instead of three systems guessing.
///
/// Everything is a pure function of a county grid coordinate: no state, no
/// allocation per query, identical results in every chunk and every session.
/// </summary>
public static class CountyTerrain
{
    /// <summary>Every road polyline in the county, macro and local.</summary>
    public static readonly CountyRoadDefinition[] AllRoads = BuildRoadTable();

    private static readonly Vector2[] MillCreekBlockout =
    [
        new(190, 214), new(181, 220), new(176, 230), new(166, 238),
        new(159, 248), new(151, 257), new(142, 269), new(129, 277)
    ];

    /// <summary>Mill Creek as drawn, with the same meander treatment as the river.</summary>
    public static readonly Vector2[] MillCreek = CountyMacroLayout.Meander(MillCreekBlockout, 5, 1.15f, .83f);

    public static readonly Vector2[] OldMillTributary = CountyMacroLayout.Meander(
    [
        new(150, 111), new(158, 116), new(166, 121), new(174, 126), new(183, 130)
    ], 4, .8f, .91f);

    /// <summary>
    /// Cleared, inhabited ground: the settlement floor around the camp and the
    /// rural buildings that share it. Vegetation stays out, wear creeps in.
    /// </summary>
    public static readonly (Vector2 Center, Vector2 Radius, float Strength)[] Clearings =
    [
        (new Vector2(203.0f, 157.5f), new Vector2(8.5f, 7.0f), 1.00f),    // starting camp
        (new Vector2(220.0f, 154.5f), new Vector2(7.5f, 6.5f), .88f),     // abandoned family home
        (new Vector2(201.0f, 139.5f), new Vector2(11.0f, 6.0f), .74f),    // cabin terrace
        (new Vector2(196.5f, 165.0f), new Vector2(4.5f, 3.5f), .62f),     // lane pull-in
        (new Vector2(209.0f, 149.5f), new Vector2(4.5f, 3.5f), .55f),     // north paddock
        (new Vector2(165.5f, 200.0f), new Vector2(8.0f, 6.5f), .86f),     // farm yard
        (new Vector2(154.0f, 249.0f), new Vector2(7.0f, 6.0f), .80f),     // old mill yard
        (new Vector2(105.0f, 74.0f), new Vector2(9.0f, 7.0f), .84f),      // logging camp
        (new Vector2(226.0f, 190.0f), new Vector2(5.5f, 4.5f), .78f)      // service station
    ];

    // ---------------------------------------------------------------- noise

    public static float Hash01(int x, int y, int salt)
    {
        unchecked
        {
            uint value = (uint)(x * 374761393 + y * 668265263 + salt * 69069);
            value = (value ^ (value >> 13)) * 1274126177u;
            return (value ^ (value >> 16)) / (float)uint.MaxValue;
        }
    }

    /// <summary>Smooth deterministic value noise in grid space.</summary>
    public static float Noise(Vector2 point, float frequency, int salt)
    {
        float x = point.X * frequency;
        float y = point.Y * frequency;
        int ix = Mathf.FloorToInt(x);
        int iy = Mathf.FloorToInt(y);
        float fx = x - ix;
        float fy = y - iy;
        fx = fx * fx * (3f - 2f * fx);
        fy = fy * fy * (3f - 2f * fy);
        float a = Hash01(ix, iy, salt);
        float b = Hash01(ix + 1, iy, salt);
        float c = Hash01(ix, iy + 1, salt);
        float d = Hash01(ix + 1, iy + 1, salt);
        float top = a + (b - a) * fx;
        float bottom = c + (d - c) * fx;
        return top + (bottom - top) * fy;
    }

    /// <summary>Two-octave noise; the large octave clusters, the small breaks edges.</summary>
    public static float Fbm(Vector2 point, float frequency, int salt) =>
        Noise(point, frequency, salt) * .68f + Noise(point, frequency * 2.7f, salt + 91) * .32f;

    /// <summary>
    /// First sample position at or before <paramref name="edge"/> on a lattice
    /// of the given step anchored at the county origin.
    ///
    /// Scatter passes must share one county-wide lattice. Starting each chunk's
    /// loop at its own origin looks harmless, but chunks are 32 cells and most
    /// steps do not divide 32, so every chunk got a different lattice phase and
    /// the change of rhythm was plainly visible as a straight seam along chunk
    /// boundaries while panning.
    /// </summary>
    public static int LatticeStart(float edge, int step) =>
        Mathf.FloorToInt(Mathf.Floor(edge / step) * step);

    // ------------------------------------------------------------- geometry

    public static float Influence(Vector2 point, Vector2 center, Vector2 radius)
    {
        Vector2 offset = point - center;
        float distance = Mathf.Sqrt(
            offset.X / radius.X * (offset.X / radius.X) +
            offset.Y / radius.Y * (offset.Y / radius.Y));
        float value = Mathf.Clamp(1f - distance, 0f, 1f);
        return value * value * (3f - 2f * value);
    }

    public static float DistanceToPolyline(Vector2 point, Vector2[] line)
    {
        float best = float.PositiveInfinity;
        for (int index = 0; index < line.Length - 1; index++)
            best = Mathf.Min(best, DistanceToSegment(point, line[index], line[index + 1]));
        return best;
    }

    public static float DistanceToSegment(Vector2 point, Vector2 start, Vector2 end)
    {
        Vector2 segment = end - start;
        float lengthSquared = segment.LengthSquared();
        if (lengthSquared <= .0001f)
            return point.DistanceTo(start);
        float t = Mathf.Clamp((point - start).Dot(segment) / lengthSquared, 0f, 1f);
        return point.DistanceTo(start + segment * t);
    }

    public static bool IsInLake(Vector2 point) =>
        LakeBounds.HasPoint(point) && Geometry2D.IsPointInPolygon(point, CountyMacroLayout.BlackwaterLakeOutline);

    /// <summary>
    /// Road bounding boxes, grown by their useful reach. Surface classification
    /// and vegetation suppression both sample per cell, so rejecting the ~25
    /// roads that are nowhere near a point matters more than it looks.
    /// </summary>
    private static readonly Rect2[] RoadBounds = BuildRoadBounds();

    private static Rect2[] BuildRoadBounds()
    {
        Rect2[] bounds = new Rect2[AllRoads.Length];
        for (int index = 0; index < AllRoads.Length; index++)
        {
            CountyRoadDefinition road = AllRoads[index];
            Rect2 box = new(road.Points[0], Vector2.Zero);
            foreach (Vector2 point in road.Points)
                box = box.Expand(point);
            bounds[index] = box.Grow(road.HalfWidth + 5f);
        }
        return bounds;
    }

    /// <summary>Distance to the nearest road centre line, in grid cells.</summary>
    public static float DistanceToRoad(Vector2 point)
    {
        float distance = float.PositiveInfinity;
        for (int index = 0; index < AllRoads.Length; index++)
        {
            if (!RoadBounds[index].HasPoint(point))
                continue;
            distance = Mathf.Min(distance, DistanceToPolyline(point, AllRoads[index].Points));
        }
        return distance;
    }

    /// <summary>Road proximity: 1 on the centre line, 0 outside the shoulder.</summary>
    public static float RoadInfluence(Vector2 point, out CountyRoadDefinition? nearest)
    {
        nearest = null;
        float best = 0f;
        for (int index = 0; index < AllRoads.Length; index++)
        {
            if (!RoadBounds[index].HasPoint(point))
                continue;
            CountyRoadDefinition road = AllRoads[index];
            float distance = DistanceToPolyline(point, road.Points);
            // Reach comes from the road's class, not a flat constant.
            //
            // Every route used to wear a band 2.6 cells wider than its half
            // width, so a farm lane painted eight cells of bare earth for a two
            // cell track. The authored road art then sat in the middle of a
            // dirt platform far wider than itself, which is most of why the
            // pieces read as sprites pasted onto the ground.
            float reach = CountyRoadClasses.ProfileOf(road).VergeReach + road.HalfWidth * .35f;
            if (distance >= reach)
                continue;
            float value = 1f - distance / reach;
            if (value > best)
            {
                best = value;
                nearest = road;
            }
        }
        return best;
    }

    private static readonly Vector2[] LakeEdge =
        [.. CountyMacroLayout.BlackwaterLakeOutline, CountyMacroLayout.BlackwaterLakeOutline[0]];

    private static readonly Rect2 LakeBounds = BuildLakeBounds();

    private static Rect2 BuildLakeBounds()
    {
        Rect2 box = new(CountyMacroLayout.BlackwaterLakeOutline[0], Vector2.Zero);
        foreach (Vector2 point in CountyMacroLayout.BlackwaterLakeOutline)
            box = box.Expand(point);
        return box.Grow(8);
    }

    /// <summary>Distance to any open water, lake shoreline included.</summary>
    public static float DistanceToWater(Vector2 point)
    {
        float best = Mathf.Min(
            DistanceToPolyline(point, CountyMacroLayout.BlackwaterRiverCourse),
            Mathf.Min(DistanceToPolyline(point, MillCreek), DistanceToPolyline(point, OldMillTributary)));
        if (LakeBounds.HasPoint(point))
            best = Mathf.Min(best, DistanceToPolyline(point, LakeEdge));
        return best;
    }

    /// <summary>Distance to the lake shoreline; positive-infinity when far away.</summary>
    public static float DistanceToLakeShore(Vector2 point) =>
        LakeBounds.HasPoint(point) ? DistanceToPolyline(point, LakeEdge) : float.PositiveInfinity;

    /// <summary>0 in open country, 1 at the heart of an inhabited clearing.</summary>
    public static float ClearingInfluence(Vector2 point)
    {
        float best = 0f;
        foreach ((Vector2 center, Vector2 radius, float strength) in Clearings)
            best = Mathf.Max(best, Influence(point, center, radius) * strength);
        return best;
    }

    // --------------------------------------------------------------- biomes

    public static CountyBiome BiomeAt(Vector2 point)
    {
        if (IsInLake(point)) return CountyBiome.Water;
        float mill = Influence(point, new Vector2(154, 250), new Vector2(49, 43));
        float farm = Influence(point, new Vector2(170, 204), new Vector2(55, 45));
        float outskirts = Influence(point, new Vector2(197, 157), new Vector2(48, 40));
        float urban = Influence(point, new Vector2(252, 145), new Vector2(54, 45));
        float south = Influence(point, new Vector2(164, 263), new Vector2(91, 59));
        float logging = Influence(point, new Vector2(105, 74), new Vector2(31, 24));
        float pine = Influence(point, new Vector2(72, 37), new Vector2(67, 37));
        float trailer = Influence(point, new Vector2(279, 211), new Vector2(32, 25));
        float fairgrounds = Influence(point, new Vector2(246, 234), new Vector2(41, 30));
        if (logging > .25f) return CountyBiome.Logging;
        if (pine > .24f) return CountyBiome.PineRidge;
        if (mill > .34f) return CountyBiome.Mill;
        if (farm > .37f) return CountyBiome.Farm;
        if (outskirts > .38f) return CountyBiome.Outskirts;
        if (urban > .38f) return CountyBiome.Urban;
        if (trailer > .30f) return CountyBiome.TrailerPark;
        if (fairgrounds > .30f) return CountyBiome.Fairgrounds;
        if (south > .35f) return CountyBiome.SouthFarm;
        if (point.Y < 118 || point.X < 115) return CountyBiome.Forest;
        if (point.X > 290) return CountyBiome.Scrub;
        return CountyBiome.Meadow;
    }

    /// <summary>
    /// Low-frequency regional colour. Ground diamonds are tinted towards this so
    /// neighbouring regions read as one continuous landscape rather than a
    /// patchwork of unrelated tile packs.
    /// </summary>
    public static Color RegionColor(Vector2 point)
    {
        Color color = new("#49613b");
        color = color.Lerp(new Color("#304735"), Influence(point, new Vector2(145, 54), new Vector2(170, 76)) * .83f);
        color = color.Lerp(new Color("#263b31"), Influence(point, new Vector2(72, 37), new Vector2(69, 39)) * .82f);
        color = color.Lerp(new Color("#604f35"), Influence(point, new Vector2(105, 74), new Vector2(31, 24)) * .72f);
        color = color.Lerp(new Color("#607848"), Influence(point, new Vector2(197, 157), new Vector2(51, 43)) * .78f);
        color = color.Lerp(new Color("#737747"), Influence(point, new Vector2(170, 204), new Vector2(62, 51)) * .82f);
        color = color.Lerp(new Color("#304e3b"), Influence(point, new Vector2(154, 250), new Vector2(53, 48)) * .88f);
        color = color.Lerp(new Color("#777849"), Influence(point, new Vector2(164, 268), new Vector2(100, 66)) * .72f);
        color = color.Lerp(new Color("#565a50"), Influence(point, new Vector2(252, 145), new Vector2(57, 48)) * .84f);
        color = color.Lerp(new Color("#5f5b4d"), Influence(point, new Vector2(279, 211), new Vector2(32, 25)) * .73f);
        color = color.Lerp(new Color("#78764a"), Influence(point, new Vector2(246, 234), new Vector2(41, 30)) * .68f);
        color = color.Lerp(new Color("#46593b"), Influence(point, new Vector2(322, 193), new Vector2(77, 105)) * .58f);
        return color;
    }

    // -------------------------------------------------------------- surface

    /// <summary>
    /// The painted ground material at a point. Ordering matters: human traffic
    /// and water override the regional default, which is itself broken up by
    /// clustered moisture/fertility noise rather than per-cell randomness.
    /// </summary>
    public static GroundSurface SurfaceAt(Vector2 point)
    {
        if (IsInLake(point))
            return GroundSurface.None;

        CountyBiome biome = BiomeAt(point);

        // Banks read as damp ground rather than a hard colour edge.
        float water = DistanceToWater(point);
        if (water < 1.9f)
            return GroundSurface.Mud;
        if (water < 3.4f)
            return Fbm(point, .40f, 311) > .45f ? GroundSurface.Wetland : GroundSurface.Mud;

        // Every threshold below is dithered by the same fine noise field.
        //
        // Without it, each surface boundary lands on a clean contour, and since
        // the ground is painted in two-cell diamonds that contour reads as a
        // hard staircase of tile edges. Perturbing the test instead lets the two
        // surfaces interleave for a tile or two, which is what makes a verge
        // fade into a meadow rather than stopping against it.
        float dither = (Fbm(point, .42f, 733) - .5f) * .30f;

        // Road verges: the carriageway itself is painted by the road layer, but
        // the ground either side is worn rather than untouched meadow.
        float roadEdge = RoadInfluence(point, out CountyRoadDefinition? road);
        if (roadEdge > .01f && road is not null)
        {
            float verge = roadEdge * (road.Major ? 1.15f : .95f) + dither;
            if (verge > .66f)
                return road.Major ? GroundSurface.Gravel : GroundSurface.BareEarth;
            if (verge > .34f)
                return GroundSurface.Trodden;
        }

        // Inhabited clearings wear down to bare yard near their centre.
        float clearing = ClearingInfluence(point);
        if (clearing > .001f)
        {
            float wear = clearing + dither;
            if (wear > .80f)
                return biome is CountyBiome.Logging or CountyBiome.Mill ? GroundSurface.Mud : GroundSurface.BareEarth;
            if (wear > .52f)
                return GroundSurface.Trodden;
            if (wear > .24f)
                return GroundSurface.Pasture;
        }

        float fertility = Fbm(point, .085f, 17) + dither * .5f;
        float damp = Fbm(point, .062f, 53) + dither * .5f;

        return biome switch
        {
            CountyBiome.Urban => fertility > .58f ? GroundSurface.Trodden : GroundSurface.Gravel,
            CountyBiome.TrailerPark => fertility > .55f ? GroundSurface.DryGrass : GroundSurface.Gravel,
            CountyBiome.Fairgrounds => fertility > .5f ? GroundSurface.DryGrass : GroundSurface.Pasture,
            CountyBiome.Scrub => fertility > .56f ? GroundSurface.Scrub : GroundSurface.DryGrass,
            CountyBiome.PineRidge => damp > .56f ? GroundSurface.PineFloor
                : fertility > .58f ? GroundSurface.ForestFloor : GroundSurface.PineFloor,
            CountyBiome.Logging => fertility > .60f ? GroundSurface.ForestFloor
                : damp > .55f ? GroundSurface.Mud : GroundSurface.BareEarth,
            CountyBiome.Forest => fertility > .48f ? GroundSurface.ForestFloor
                : damp > .58f ? GroundSurface.Wetland : GroundSurface.Meadow,
            CountyBiome.Mill => damp > .52f ? GroundSurface.Wetland
                : fertility > .5f ? GroundSurface.ForestFloor : GroundSurface.Meadow,
            CountyBiome.Farm or CountyBiome.SouthFarm => FieldIndex(point) is int field and >= 0
                ? FieldSurface(field, fertility)
                : fertility > .58f ? GroundSurface.RichMeadow : GroundSurface.Pasture,
            CountyBiome.Outskirts => fertility > .70f ? GroundSurface.RichMeadow
                : fertility > .30f ? GroundSurface.Meadow
                : GroundSurface.Pasture,
            _ => fertility > .64f ? GroundSurface.RichMeadow
                : fertility > .30f ? GroundSurface.Meadow : GroundSurface.Pasture
        };
    }

    /// <summary>Cultivated field footprints, shared with the farm composition pass.</summary>
    public static readonly Rect2[] Fields =
    [
        new(134, 174, 19, 27), new(156, 176, 28, 18), new(135, 205, 23, 25),
        new(161, 211, 29, 19), new(174, 195, 18, 13), new(104, 238, 30, 31),
        new(177, 241, 31, 30), new(75, 248, 24, 29), new(79, 282, 31, 24),
        new(115, 274, 27, 32), new(147, 280, 25, 29), new(177, 277, 34, 27),
        new(214, 254, 27, 31)
    ];

    public static bool IsInField(Vector2 point) => FieldIndex(point) >= 0;

    public static int FieldIndex(Vector2 point)
    {
        for (int index = 0; index < Fields.Length; index++)
            if (Fields[index].HasPoint(point))
                return index;
        return -1;
    }

    /// <summary>
    /// Each field commits to one state for the season. Deciding this per field
    /// rather than per cell is what turns the agricultural belt into a legible
    /// mosaic of plough, crop and fallow instead of uniform corduroy.
    /// </summary>
    public static FieldState StateOfField(int index) =>
        (FieldState)(int)(Hash01(index * 31, index * 17 + 5, 1777) * 4f % 4f);

    private static GroundSurface FieldSurface(int index, float fertility) => StateOfField(index) switch
    {
        FieldState.Ploughed => fertility > .62f ? GroundSurface.BareEarth : GroundSurface.Ploughed,
        FieldState.Sown => fertility > .48f ? GroundSurface.Farmland : GroundSurface.Ploughed,
        FieldState.Standing => fertility > .40f ? GroundSurface.Farmland : GroundSurface.Pasture,
        _ => fertility > .55f ? GroundSurface.DryGrass : GroundSurface.Pasture
    };

    /// <summary>
    /// How deeply shaded the ground is, 0 in the open and 1 under closed canopy.
    ///
    /// This is the single biggest lever for macro hierarchy. Woodland floors sit
    /// in shade and open country is bright, so at a glance the player reads
    /// forest, edge and clearing as three distinct masses before noticing any
    /// individual ground detail. It uses the same mass noise as the vegetation
    /// pass, so the shade and the trees casting it always agree.
    /// </summary>
    public static float CanopyShade(Vector2 point)
    {
        float density = BiomeAt(point) switch
        {
            CountyBiome.PineRidge => 1.00f,
            CountyBiome.Forest => .88f,
            CountyBiome.Mill => .80f,
            CountyBiome.Logging => .55f,
            CountyBiome.Outskirts => .34f,
            CountyBiome.Scrub => .18f,
            CountyBiome.Meadow => .16f,
            _ => .06f
        };
        if (density <= 0f)
            return 0f;
        float mass = Fbm(point, .055f, 1201);
        float shade = density * (.45f + mass * 1.25f) - VegetationSuppression(point);
        return Mathf.Clamp(shade, 0f, 1f);
    }

    /// <summary>
    /// How strongly vegetation should be suppressed here: roads, yards, fields
    /// and water margins all push plants out so gameplay space stays readable.
    /// </summary>
    public static float VegetationSuppression(Vector2 point)
    {
        float suppression = 0f;

        float roadDistance = DistanceToRoad(point);
        if (roadDistance < 3.4f)
            suppression = Mathf.Max(suppression, Mathf.Clamp(1f - (roadDistance - .9f) / 2.5f, 0f, 1f));

        suppression = Mathf.Max(suppression, ClearingInfluence(point) * 1.18f);

        if (IsInField(point))
            suppression = Mathf.Max(suppression, .96f);

        if (DistanceToWater(point) < 1.6f)
            suppression = Mathf.Max(suppression, .9f);

        return Mathf.Clamp(suppression, 0f, 1f);
    }

    // ---------------------------------------------------------------- table

    private static CountyRoadDefinition[] BuildRoadTable()
    {
        List<CountyRoadDefinition> roads = [.. CountyMacroLayout.Roads];

        // Local farm and mill access. These are the tracks the settlement
        // actually uses, so they carry wear and roadside dressing too.
        roads.Add(new CountyRoadDefinition("farm_north_track", "North Field Track", .52f,
            [new(145, 180), new(157, 184), new(170, 188), new(184, 192)]));
        roads.Add(new CountyRoadDefinition("farm_west_track", "West Field Track", .46f,
            [new(151, 180), new(151, 197), new(148, 216), new(155, 231), new(160, 233)]));
        roads.Add(new CountyRoadDefinition("farmyard_track", "Farmyard Track", .56f,
            [new(183, 191), new(173, 198), new(164, 204), new(155, 211)]));
        roads.Add(new CountyRoadDefinition("mill_logging_track", "Mill Logging Track", .48f,
            [new(154, 250), new(143, 246), new(132, 247), new(122, 254), new(117, 264), new(116, 272)]));

        // The outskirts lane the starting camp actually sits on, and the field
        // track running north from it. These were previously drawn by the local
        // TerrainRenderer as flat ribbons; owning them here lets the county
        // road, wear and vegetation-exclusion passes treat them properly.
        roads.Add(new CountyRoadDefinition("outskirts_lane", "Outskirts Lane", .92f,
            [new(189, 179), new(186, 174), new(190, 168), new(196, 166), new(202, 164), new(208, 163), new(214, 160), new(220, 156), new(229, 153)]));
        roads.Add(new CountyRoadDefinition("outskirts_field_track", "Outskirts Field Track", .46f,
            [new(196, 166), new(195, 161), new(194, 155), new(192, 148), new(191, 146)]));

        // One short worn spur, east to the abandoned house. The camp's other
        // connection is farm_mill_road, which already starts on its doorstep;
        // adding more here just crowds the outskirts with parallel tracks.
        roads.Add(new CountyRoadDefinition("camp_lane_spur", "Camp Spur", .40f,
            [new(203, 157), new(205, 160), new(207, 163)]));
        roads.Add(new CountyRoadDefinition("camp_house_path", "Farmhouse Path", .30f,
            [new(203, 157), new(207, 156), new(211, 155), new(215, 155)]));

        // Ashwood's street plan: connected but deliberately incomplete.
        roads.Add(new CountyRoadDefinition("ashwood_main", "Ashwood Main Street", 1.05f,
            [new(216, 143), new(292, 143)]));
        roads.Add(new CountyRoadDefinition("ashwood_north", "North Ashwood Street", .68f,
            [new(216, 122), new(291, 122)]));
        roads.Add(new CountyRoadDefinition("ashwood_south", "South Ashwood Street", .68f,
            [new(216, 166), new(291, 166)]));
        foreach (int x in new[] { 216, 237, 270, 291 })
            roads.Add(new CountyRoadDefinition($"ashwood_cross_{x}", "Ashwood Cross Street", .68f,
                [new(x, 118), new(x, 173)]));

        // Minor routes are given a slight wander.
        //
        // A track laid down as three or four control points renders as a set of
        // dead-straight segments with mathematically parallel edges, which is
        // the single strongest tell that a road was generated rather than built.
        // Real farm and forest tracks follow ground they did not choose. The
        // amplitude is small enough not to move a route off its junctions, and
        // the highway and town grid are deliberately left alone because
        // engineered roads genuinely are straight.
        for (int index = 0; index < roads.Count; index++)
        {
            CountyRoadDefinition road = roads[index];
            if (road.Major || road.Id.StartsWith("ashwood_", StringComparison.Ordinal))
                continue;
            float amplitude = road.HalfWidth < .5f ? .85f : road.HalfWidth < 1f ? .70f : .55f;
            roads[index] = road with
            {
                Points = CountyMacroLayout.Meander(road.Points, 4, amplitude, .61f + index * .017f)
            };
        }

        return [.. roads];
    }

    /// <summary>A place where two roads cross, and whether the crossing is paved.</summary>
    public readonly record struct RoadJunction(Vector2 Position, bool Paved);

    /// <summary>
    /// Every crossing in the network, worked out once from the road geometry.
    ///
    /// Ribbons drawn independently simply overlap where they meet, which leaves
    /// the join looking like two strips laid on top of each other. Knowing where
    /// the crossings are lets the landscape pass stamp the library's authored
    /// junction tiles over them, so an intersection reads as a built thing.
    /// </summary>
    public static readonly RoadJunction[] Junctions = BuildJunctions();

    private static RoadJunction[] BuildJunctions()
    {
        List<RoadJunction> junctions = [];
        for (int i = 0; i < AllRoads.Length; i++)
        {
            for (int j = i + 1; j < AllRoads.Length; j++)
            {
                if (!RoadBounds[i].Intersects(RoadBounds[j]))
                    continue;
                Vector2[] a = AllRoads[i].Points;
                Vector2[] b = AllRoads[j].Points;
                for (int p = 0; p < a.Length - 1; p++)
                {
                    for (int q = 0; q < b.Length - 1; q++)
                    {
                        Variant crossing = Geometry2D.SegmentIntersectsSegment(a[p], a[p + 1], b[q], b[q + 1]);
                        if (crossing.VariantType != Variant.Type.Vector2)
                            continue;
                        Vector2 at = crossing.AsVector2();
                        // Roads that share an endpoint meet many times over a
                        // short run; one stamp per junction is enough.
                        bool duplicate = false;
                        foreach (RoadJunction existing in junctions)
                        {
                            if (existing.Position.DistanceSquaredTo(at) < 9f) { duplicate = true; break; }
                        }
                        if (duplicate)
                            continue;
                        bool paved = AllRoads[i].Major || AllRoads[j].Major
                            || AllRoads[i].Id.StartsWith("ashwood_", StringComparison.Ordinal)
                            || AllRoads[j].Id.StartsWith("ashwood_", StringComparison.Ordinal);
                        junctions.Add(new RoadJunction(at, paved));
                    }
                }
            }
        }
        return [.. junctions];
    }
}
