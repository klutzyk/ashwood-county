#nullable enable

using System.Collections.Generic;
using Godot;

namespace AshwoodCounty.World.County.Visual;

/// <summary>
/// Turns a logical road route into a sequence of authored road pieces.
///
/// The logical network is left exactly as it is. Navigation, region entry,
/// vegetation suppression, surface wear and the Authoring Studio all keep
/// reading the same polylines they always did. What changes is only how the
/// route is drawn: instead of sweeping a ribbon along every wobble in the
/// spline, the route is read as a sequence of intentions and rebuilt from
/// pieces that were drawn for this projection.
///
/// A route becomes runs along the two isometric ground axes. Small lateral
/// wander inside a run is deliberately discarded, because that wander is what
/// the ribbon renderer turned into a shearing, railway-like strip; an
/// environment artist laying this out would use a straight and then a curve,
/// not two hundred slightly rotated slabs.
/// </summary>
public static class DirtRoadComposer
{
    /// <summary>
    /// Shortest run worth keeping, in cells. Below this a wobble is noise in
    /// the blockout rather than a real change of direction.
    /// </summary>
    private const float MinimumRun = 9.0f;

    /// <summary>
    /// Spacing the route is resampled to before its direction is classified.
    ///
    /// The logical routes carry a deliberate meander, added so the old ribbon
    /// would not look machine-drawn. Classifying direction sample by sample
    /// turns that meander into a rapid alternation between the two ground axes,
    /// and the composed road staircases sideways. Reading direction over a
    /// several-cell window instead sees the route's actual intent and lets the
    /// meander be expressed by the artwork rather than by the layout.
    /// </summary>
    private const float ClassifySpacing = 6.5f;

    private readonly record struct Run(RoadAxis Axis, float Start, float End, float Offset);

    /// <summary>Compose one road into placements, in draw order.</summary>
    public static List<RoadPiecePlacement> Compose(CountyRoadDefinition road)
    {
        List<RoadPiecePlacement> pieces = [];
        string straight = DirtRoadKit.StraightFor(road);
        if (!DirtRoadKit.Has(straight))
            return pieces;

        List<Run> runs = BuildRuns(Resample(road.Points, ClassifySpacing));
        if (runs.Count == 0)
            return pieces;

        float along = DirtRoadKit.AlongCells(straight);
        float across = DirtRoadKit.AcrossCells(straight);
        int salt = road.Id.GetHashCode();

        for (int index = 0; index < runs.Count; index++)
        {
            Run run = runs[index];
            float length = Mathf.Abs(run.End - run.Start);
            int count = Mathf.Max(1, Mathf.RoundToInt(length / along));
            float step = length / count;
            float direction = run.End >= run.Start ? 1f : -1f;

            for (int piece = 0; piece < count; piece++)
            {
                float at = run.Start + direction * step * piece;
                Vector2 origin = run.Axis == RoadAxis.NorthEast
                    ? new Vector2(run.Offset, direction > 0 ? at + step : at)
                    : new Vector2(direction > 0 ? at : at - step, run.Offset);

                // Mirroring alternates along a run. Both orientations are
                // perspective-valid, and swapping between them stops the
                // surface detail repeating on an obvious beat.
                bool mirror = CountyTerrain.Hash01(piece, salt + index, 733) > .5f;
                // Each piece is drawn a little longer than its step so its
                // end laps over its neighbour. The artwork's ends are worn
                // rather than square, so a small overlap reads as continuous
                // ground while a butt joint shows as a seam.
                pieces.Add(new RoadPiecePlacement(
                    DirtRoadKit.TexturePath(straight), origin, run.Axis,
                    step * 1.18f, across, mirror, Colors.White));
            }
        }

        return pieces;
    }

    /// <summary>
    /// Collapse a polyline into axis-aligned runs.
    ///
    /// Each sample is classified by which ground axis it is mostly travelling
    /// along; consecutive samples that agree are merged, and the run's fixed
    /// coordinate is the average of the samples in it, so the piece line sits
    /// through the middle of the original route rather than at one end of its
    /// wander.
    /// </summary>
    /// <summary>Even resampling, so direction is judged over a fixed distance.</summary>
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

    private static List<Run> BuildRuns(Vector2[] points)
    {
        List<Run> runs = [];
        if (points.Length < 2)
            return runs;

        RoadAxis? currentAxis = null;
        float runStart = 0f;
        float offsetSum = 0f;
        int offsetCount = 0;
        Vector2 previous = points[0];

        // A run shorter than the minimum is not dropped: dropping it leaves a
        // hole in the road. It is absorbed into the previous run of the same
        // axis instead, which is what keeps the composed route continuous.
        void Flush(float end)
        {
            if (currentAxis is not RoadAxis axis || offsetCount == 0)
                return;
            float offset = offsetSum / offsetCount;
            if (Mathf.Abs(end - runStart) >= MinimumRun)
            {
                runs.Add(new Run(axis, runStart, end, offset));
                return;
            }
            for (int index = runs.Count - 1; index >= 0; index--)
            {
                if (runs[index].Axis != axis)
                    continue;
                runs[index] = runs[index] with { End = end };
                return;
            }
        }

        for (int index = 1; index < points.Length; index++)
        {
            Vector2 point = points[index];
            Vector2 delta = point - previous;
            if (delta.LengthSquared() < .0001f)
                continue;

            // Grid X moves the piece south-east on screen, grid Y north-east.
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
        return runs;
    }
}
