#nullable enable

using System.Collections.Generic;
using Godot;

namespace AshwoodCounty.World.County.Visual;

/// <summary>
/// Rebuilds the county's dirt routes out of authored road pieces.
///
/// The logical network is untouched. Navigation, region entry, vegetation
/// suppression, road queries and the Authoring Studio all keep reading the same
/// polylines. Only the drawing changes.
///
/// Composition happens once for the whole county and is cached, for two
/// reasons. It has to be, because a junction is where two roads' composed
/// centre lines meet and that cannot be known while looking at one road; and it
/// wants to be, because the result is deterministic and every chunk can then
/// just read the slices that fall inside it.
///
/// The geometry is built in one order so that everything agrees:
///
///   1. each route becomes runs along the two ground axes
///   2. a run's centre line is a single fixed coordinate
///   3. corners are where one run's line meets the next run's line
///   4. junctions are where two different roads' run lines meet
///   5. curves and junction pieces claim a radius around those points
///   6. straights are sliced into the gaps that remain
///
/// Because corners and junctions are both derived from the same run lines, a
/// junction sits exactly on the roads that meet there. The previous version
/// took junctions from the raw spline crossing while the straights snapped to
/// their run's average coordinate, so the two disagreed by a cell or two and
/// every intersection looked pasted on.
/// </summary>
public static class DirtRoadComposer
{
    /// <summary>
    /// Shortest run worth keeping.
    ///
    /// This is deliberately short. A near-diagonal route alternates between the
    /// two ground axes every few cells, and the previous behaviour, absorbing a
    /// too-short run into the last run that happened to share its axis, moved
    /// that run's extent to a completely different centre line and left the
    /// ground between them bare. Short runs are now kept, and the corner
    /// extension below is what joins them into a continuous staircase.
    /// </summary>
    private const float MinimumRun = 6.0f;

    /// <summary>
    /// Both neighbours must be at least this long before a corner earns an
    /// authored curve. On a fine staircase the runs are shorter than the curve
    /// artwork itself, so a curve there would swallow the road rather than
    /// describe a bend.
    /// </summary>
    private const float CurveMinimumRun = 6.5f;

    /// <summary>Spacing the route is resampled to before direction is judged.</summary>
    private const float ClassifySpacing = 10.0f;

    /// <summary>
    /// Two consecutive runs on the same axis whose centre lines are closer than
    /// this are one run.
    ///
    /// Judging direction over a window still leaves a route flicking axis for a
    /// few cells and back, which produced a run a cell or two to the side of its
    /// neighbour and a road that stepped sideways repeatedly. Collapsing those
    /// into a single averaged centre line is what keeps a nominally straight
    /// road straight.
    /// </summary>
    private const float CollinearOffset = 2.6f;

    /// <summary>
    /// Target length of one surface slice, in cells.
    ///
    /// Short enough that each slice can take a different window of the source
    /// and the run never repeats visibly; long enough that a run is not made of
    /// hundreds of quads.
    /// </summary>
    private const float SliceCells = 2.3f;

    private readonly record struct Run(RoadAxis Axis, float Lo, float Hi, float Offset);

    private readonly record struct Cover(Vector2 Centre, float Radius);

    private static List<RoadPiecePlacement>? _placements;
    private static Rect2[]? _bounds;

    /// <summary>Every composed placement in the county, built once.</summary>
    public static IReadOnlyList<RoadPiecePlacement> Placements
    {
        get
        {
            Build();
            return _placements!;
        }
    }

    /// <summary>Discard the cache. Only needed if the logical network changes.</summary>
    public static void Invalidate()
    {
        _placements = null;
        _bounds = null;
    }

    private static void Build()
    {
        if (_placements is not null)
            return;

        List<RoadPiecePlacement> placements = [];
        Dictionary<string, List<Run>> byRoad = [];
        Dictionary<string, CountyRoadDefinition> roads = [];

        foreach (CountyRoadDefinition road in CountyTerrain.AllRoads)
        {
            if (!CountyRoadClasses.UsesDirtKit(road))
                continue;
            List<Run> runs = BuildRuns(Resample(road.Points, ClassifySpacing));
            if (runs.Count == 0)
                continue;
            byRoad[road.Id] = runs;
            roads[road.Id] = road;
        }

        // Corners inside a route, and junctions between routes, both expressed
        // as points on the composed run lines.
        Dictionary<string, List<Cover>> covers = [];
        foreach ((string id, List<Run> runs) in byRoad)
            covers[id] = [];

        foreach ((string id, List<Run> runs) in byRoad)
        {
            RoadClassProfile profile = CountyRoadClasses.ProfileOf(roads[id]);
            for (int index = 0; index + 1 < runs.Count; index++)
            {
                if (runs[index].Axis == runs[index + 1].Axis)
                    continue;
                Vector2 corner = LineMeet(runs[index], runs[index + 1]);
                if (!CountyCoordinateSpace.Contains(corner))
                    continue;
                if (profile.Curve.Length == 0 || !DirtRoadKit.HasSprite(profile.Curve))
                    continue;
                if (runs[index].Hi - runs[index].Lo < CurveMinimumRun
                    || runs[index + 1].Hi - runs[index + 1].Lo < CurveMinimumRun)
                    continue;

                float radius = DirtRoadKit.SpriteCoverCells(profile.Curve, profile.WidthCells);
                covers[id].Add(new Cover(corner, radius));
                placements.Add(Sprite(profile.Curve, corner, profile.WidthCells, CurveMirror(runs[index], runs[index + 1])));
            }
        }

        AddJunctions(byRoad, roads, covers, placements);

        foreach ((string id, List<Run> runs) in byRoad)
            AddSlices(roads[id], runs, covers[id], placements);

        _placements = placements;
        _bounds = new Rect2[placements.Count];
        for (int index = 0; index < placements.Count; index++)
            _bounds[index] = BoundsOf(placements[index]);
    }

    /// <summary>
    /// Junctions, taken where two roads' composed run lines cross.
    ///
    /// Using the run lines rather than the raw splines is what makes the
    /// junction land on the road as drawn. The arm count decides the piece: a
    /// route that ends here contributes one arm, one that passes through
    /// contributes two.
    /// </summary>
    private static void AddJunctions(
        Dictionary<string, List<Run>> byRoad,
        Dictionary<string, CountyRoadDefinition> roads,
        Dictionary<string, List<Cover>> covers,
        List<RoadPiecePlacement> placements)
    {
        List<(Vector2 Point, string Owner, int Arms)> found = [];

        foreach ((string idA, List<Run> runsA) in byRoad)
        {
            foreach ((string idB, List<Run> runsB) in byRoad)
            {
                if (string.CompareOrdinal(idA, idB) >= 0)
                    continue;
                foreach (Run a in runsA)
                {
                    foreach (Run b in runsB)
                    {
                        if (a.Axis == b.Axis)
                            continue;
                        Vector2 point = LineMeet(a, b);
                        if (!Within(a, b, point) || !CountyCoordinateSpace.Contains(point))
                            continue;
                        bool duplicate = false;
                        foreach ((Vector2 existing, _, _) in found)
                        {
                            if (existing.DistanceSquaredTo(point) < 16f) { duplicate = true; break; }
                        }
                        if (duplicate)
                            continue;

                        // The wider of the two roads owns the junction, so a
                        // farm lane meeting a county road gets the county
                        // road's junction art rather than the lane's.
                        string owner = CountyRoadClasses.ProfileOf(roads[idA]).WidthCells
                                       >= CountyRoadClasses.ProfileOf(roads[idB]).WidthCells ? idA : idB;
                        int arms = ArmsAt(point, byRoad);
                        found.Add((point, owner, arms));
                    }
                }
            }
        }

        foreach ((Vector2 point, string owner, int arms) in found)
        {
            RoadClassProfile profile = CountyRoadClasses.ProfileOf(roads[owner]);
            string piece = arms >= 4 ? profile.JunctionFour : profile.JunctionThree;
            if (piece.Length == 0 || !DirtRoadKit.HasSprite(piece))
                continue;

            float radius = DirtRoadKit.SpriteCoverCells(piece, profile.WidthCells);
            // Every road meeting here keeps clear, so the junction art is the
            // only thing drawn at the crossing and the arms read as one piece.
            foreach ((string id, List<Run> runs) in byRoad)
            {
                foreach (Run run in runs)
                {
                    if (DistanceToRun(run, point) > profile.WidthCells * 1.2f)
                        continue;
                    covers[id].Add(new Cover(point, radius * .92f));
                    break;
                }
            }
            placements.Add(Sprite(piece, point, profile.WidthCells,
                CountyTerrain.Hash01((int)point.X, (int)point.Y, 331) > .5f));
        }
    }

    /// <summary>Lay contiguous surface slices along every run, skipping covered ground.</summary>
    private static void AddSlices(
        CountyRoadDefinition road, List<Run> runs, List<Cover> covers, List<RoadPiecePlacement> placements)
    {
        RoadClassProfile profile = CountyRoadClasses.ProfileOf(road);
        if (profile.Straights.Length == 0)
            return;
        int salt = road.Id.GetHashCode();

        for (int runIndex = 0; runIndex < runs.Count; runIndex++)
        {
            Run run = runs[runIndex];
            float length = run.Hi - run.Lo;
            if (length <= .01f)
                continue;

            int count = Mathf.Max(1, Mathf.RoundToInt(length / SliceCells));
            float step = length / count;

            for (int slice = 0; slice < count; slice++)
            {
                float lo = run.Lo + step * slice;
                float hi = lo + step;
                Vector2 centre = run.Axis == RoadAxis.NorthEast
                    ? new Vector2(run.Offset, (lo + hi) * .5f)
                    : new Vector2((lo + hi) * .5f, run.Offset);

                bool covered = false;
                foreach (Cover cover in covers)
                {
                    if (centre.DistanceTo(cover.Centre) < cover.Radius) { covered = true; break; }
                }
                if (covered)
                    continue;

                // One surface per route: continuity along a road matters more
                // than variety between its slices. The choice is seeded by the
                // road, not by the slice.
                string piece = profile.Straights[
                    (int)(CountyTerrain.Hash01(salt, runIndex, 191) * profile.Straights.Length) % profile.Straights.Length];
                if (!DirtRoadKit.HasStraight(piece))
                    continue;

                // A different window of the source for each slice, so the run
                // never repeats the same stones on a visible beat.
                float span = DirtRoadKit.SourceSpanFor(step);
                float u0 = .03f + CountyTerrain.Hash01(runIndex * 97 + slice, salt, 211) * Mathf.Max(0f, .94f - span);
                bool mirror = CountyTerrain.Hash01(runIndex, salt + slice / 3, 733) > .5f;

                // A hair of overlap past the shared edge, so no subpixel seam
                // can show between neighbours.
                const float lap = .06f;
                placements.Add(new RoadPiecePlacement(
                    RoadPieceKind.Slice,
                    DirtRoadKit.StraightPath(piece),
                    centre,
                    DirtRoadKit.SliceCorners(run.Axis, lo - lap, hi + lap, run.Offset, run.Offset, profile.WidthCells),
                    DirtRoadKit.SliceUvs(piece, u0, u0 + span, mirror),
                    1f,
                    Colors.White));
            }
        }
    }

    private static RoadPiecePlacement Sprite(string piece, Vector2 centre, float width, bool mirror) =>
        new(RoadPieceKind.Sprite,
            DirtRoadKit.SpritePath(piece),
            centre,
            [],
            mirror ? [new Vector2(1, 0), new Vector2(0, 0), new Vector2(0, 1), new Vector2(1, 1)] : [],
            DirtRoadKit.SpriteScaleFor(piece, width),
            Colors.White);

    /// <summary>Placements whose footprint touches a chunk.</summary>
    public static void CollectFor(Rect2 bounds, List<RoadPiecePlacement> output)
    {
        Build();
        for (int index = 0; index < _placements!.Count; index++)
        {
            if (_bounds![index].Intersects(bounds))
                output.Add(_placements[index]);
        }
    }

    private static Rect2 BoundsOf(RoadPiecePlacement placement)
    {
        if (placement.Kind == RoadPieceKind.Sprite)
            return new Rect2(placement.Anchor - Vector2.One * 4f, Vector2.One * 8f);
        Rect2 box = new(placement.GridCorners[0], Vector2.Zero);
        foreach (Vector2 corner in placement.GridCorners)
            box = box.Expand(corner);
        return box;
    }

    // ------------------------------------------------------------- geometry

    /// <summary>Where two perpendicular run centre lines meet.</summary>
    private static Vector2 LineMeet(Run a, Run b)
    {
        Run northEast = a.Axis == RoadAxis.NorthEast ? a : b;
        Run southEast = a.Axis == RoadAxis.NorthEast ? b : a;
        // A north-east run holds X fixed; a south-east run holds Y fixed.
        return new Vector2(northEast.Offset, southEast.Offset);
    }

    private static bool Within(Run a, Run b, Vector2 point)
    {
        Run northEast = a.Axis == RoadAxis.NorthEast ? a : b;
        Run southEast = a.Axis == RoadAxis.NorthEast ? b : a;
        return point.Y >= northEast.Lo - 1f && point.Y <= northEast.Hi + 1f
            && point.X >= southEast.Lo - 1f && point.X <= southEast.Hi + 1f;
    }

    private static float DistanceToRun(Run run, Vector2 point) => run.Axis == RoadAxis.NorthEast
        ? (point.Y >= run.Lo - 1f && point.Y <= run.Hi + 1f ? Mathf.Abs(point.X - run.Offset) : float.PositiveInfinity)
        : (point.X >= run.Lo - 1f && point.X <= run.Hi + 1f ? Mathf.Abs(point.Y - run.Offset) : float.PositiveInfinity);

    private static int ArmsAt(Vector2 point, Dictionary<string, List<Run>> byRoad)
    {
        int arms = 0;
        foreach (List<Run> runs in byRoad.Values)
        {
            foreach (Run run in runs)
            {
                if (DistanceToRun(run, point) > 2.5f)
                    continue;
                float along = run.Axis == RoadAxis.NorthEast ? point.Y : point.X;
                bool ends = along - run.Lo < 2.5f || run.Hi - along < 2.5f;
                arms += ends ? 1 : 2;
                break;
            }
        }
        return arms;
    }

    /// <summary>Which way round the curve art goes for a given pair of runs.</summary>
    private static bool CurveMirror(Run first, Run second)
    {
        Run northEast = first.Axis == RoadAxis.NorthEast ? first : second;
        Run southEast = first.Axis == RoadAxis.NorthEast ? second : first;
        Vector2 corner = new(northEast.Offset, southEast.Offset);
        // The curve's free arms point away from the corner; mirroring swaps
        // which side they leave on.
        bool northEastLeaves = northEast.Hi - corner.Y > corner.Y - northEast.Lo;
        bool southEastLeaves = southEast.Hi - corner.X > corner.X - southEast.Lo;
        return northEastLeaves == southEastLeaves;
    }

    private static Vector2[] Resample(Vector2[] points, float spacing)
    {
        List<Vector2> result = [points[0]];
        float carried = 0f;
        for (int index = 0; index < points.Length - 1; index++)
        {
            Vector2 a = points[index];
            Vector2 b = points[index + 1];
            float length = a.DistanceTo(b);
            if (length < .0001f)
                continue;
            float travelled = spacing - carried;
            while (travelled <= length)
            {
                result.Add(a.Lerp(b, travelled / length));
                travelled += spacing;
            }
            carried = (carried + length) % spacing;
        }
        result.Add(points[^1]);
        return [.. result];
    }

    /// <summary>Collapse a polyline into axis-aligned runs with fixed centre lines.</summary>
    private static List<Run> BuildRuns(Vector2[] points)
    {
        List<(RoadAxis Axis, float Start, float End, float Offset)> raw = [];
        if (points.Length < 2)
            return [];

        RoadAxis? currentAxis = null;
        float runStart = 0f;
        float offsetSum = 0f;
        int offsetCount = 0;
        Vector2 previous = points[0];

        void Flush(float end)
        {
            if (currentAxis is not RoadAxis axis || offsetCount == 0)
                return;
            float offset = offsetSum / offsetCount;
            if (Mathf.Abs(end - runStart) < .35f)
                return;
            raw.Add((axis, runStart, end, offset));
        }

        for (int index = 1; index < points.Length; index++)
        {
            Vector2 point = points[index];
            Vector2 delta = point - previous;
            if (delta.LengthSquared() < .0001f)
                continue;

            RoadAxis axis = Mathf.Abs(delta.X) >= Mathf.Abs(delta.Y) ? RoadAxis.SouthEast : RoadAxis.NorthEast;
            if (currentAxis != axis)
            {
                Flush(currentAxis == RoadAxis.SouthEast ? previous.X : previous.Y);
                currentAxis = axis;
                runStart = axis == RoadAxis.SouthEast ? previous.X : previous.Y;
                offsetSum = 0f;
                offsetCount = 0;
            }

            offsetSum += axis == RoadAxis.SouthEast ? point.Y : point.X;
            offsetCount++;
            previous = point;
        }
        Flush(currentAxis == RoadAxis.SouthEast ? previous.X : previous.Y);

        // Collapse runs that are really the same road before anything else
        // looks at them.
        for (int index = raw.Count - 2; index >= 0; index--)
        {
            if (raw[index].Axis != raw[index + 1].Axis)
                continue;
            if (Mathf.Abs(raw[index].Offset - raw[index + 1].Offset) > CollinearOffset)
                continue;
            float lo = Mathf.Min(Mathf.Min(raw[index].Start, raw[index].End),
                                 Mathf.Min(raw[index + 1].Start, raw[index + 1].End));
            float hi = Mathf.Max(Mathf.Max(raw[index].Start, raw[index].End),
                                 Mathf.Max(raw[index + 1].Start, raw[index + 1].End));
            raw[index] = (raw[index].Axis, lo, hi, (raw[index].Offset + raw[index + 1].Offset) * .5f);
            raw.RemoveAt(index + 1);
        }

        // Extend each run to its neighbours' centre lines so consecutive runs
        // actually reach the corner between them instead of stopping short and
        // leaving a wedge of grass through the bend.
        List<Run> runs = [];
        for (int index = 0; index < raw.Count; index++)
        {
            (RoadAxis axis, float start, float end, float offset) = raw[index];
            float lo = Mathf.Min(start, end);
            float hi = Mathf.Max(start, end);
            if (index > 0 && raw[index - 1].Axis != axis)
            {
                float neighbour = raw[index - 1].Offset;
                lo = Mathf.Min(lo, neighbour);
                hi = Mathf.Max(hi, neighbour);
            }
            if (index + 1 < raw.Count && raw[index + 1].Axis != axis)
            {
                float neighbour = raw[index + 1].Offset;
                lo = Mathf.Min(lo, neighbour);
                hi = Mathf.Max(hi, neighbour);
            }
            runs.Add(new Run(axis, lo, hi, offset));
        }
        return runs;
    }
}
