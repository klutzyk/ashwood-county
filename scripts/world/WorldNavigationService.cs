#nullable enable

using System.Collections.Generic;
using System.Linq;
using Godot;

namespace AshwoodCounty.World;

/// <summary>
/// Exterior obstacle layer for the continuous county. Substantial authored
/// objects (buildings, construction, collision-flagged props) register their
/// ground footprints here; movement routes sample the segment and splice
/// corner-bypass waypoints around the first blocking footprint. Interior
/// routes remain handled by InteriorNavigationService, which this service
/// deliberately does not replace.
/// </summary>
public sealed class WorldObstacle
{
    public WorldFootprint Footprint { get; init; }
    public GodotObject Owner { get; init; } = null!;

    /// <summary>True when actors are allowed to traverse the footprint once an endpoint is inside it (buildings and their doors).</summary>
    public bool AllowTraversalInside { get; init; } = true;
}

public partial class WorldNavigationService : Node
{
    public const string GroupName = "world_navigation";
    private const float ActorClearance = 0.24f;
    private const float SampleStep = 0.34f;

    private readonly List<WorldObstacle> _obstacles = [];
    private readonly Dictionary<Vector2I, List<int>> _byCell = [];

    public override void _Ready()
    {
        AddToGroup(GroupName);
    }

    public IReadOnlyList<WorldObstacle> Obstacles => _obstacles;

    public void RegisterObstacle(WorldFootprint footprint, GodotObject owner, bool allowTraversalInside = true)
    {
        if (owner is null || !footprint.IsValid || _obstacles.Any(item => item.Owner == owner))
        {
            return;
        }

        _obstacles.Add(new WorldObstacle { Footprint = footprint, Owner = owner, AllowTraversalInside = allowTraversalInside });
        int index = _obstacles.Count - 1;
        foreach (Vector2I cell in EnumerateCells(footprint.Bounds))
        {
            if (!_byCell.TryGetValue(cell, out List<int>? entries))
            {
                entries = [];
                _byCell[cell] = entries;
            }

            entries.Add(index);
        }
    }

    public void UnregisterObstacle(GodotObject owner)
    {
        if (owner is null)
        {
            return;
        }

        int index = _obstacles.FindIndex(item => item.Owner == owner);
        if (index < 0)
        {
            return;
        }

        _obstacles.RemoveAt(index);
        foreach (List<int> entries in _byCell.Values)
        {
            entries.RemoveAll(entry => entry == index);
        }

        // Rebuild indices after the removal so stored indexes stay valid.
        foreach (List<int> entries in _byCell.Values)
        {
            for (int i = 0; i < entries.Count; i++)
            {
                if (entries[i] > index) entries[i]--;
            }
        }

        foreach (Vector2I cell in _byCell.Keys.ToArray())
        {
            if (_byCell[cell].Count == 0) _byCell.Remove(cell);
        }
    }

    public bool IsBlocked(Vector2 from, Vector2 to)
    {
        return FirstBlocking(from, to) is not null;
    }

    /// <summary>
    /// Returns a polyline [from, corner..., to] that walks around the first
    /// blocking footprint. Each corner is checked recursively, so a route can
    /// navigate around a handful of overlapping footprints without a full A*.
    /// </summary>
    public IReadOnlyList<Vector2> Bypass(Vector2 from, Vector2 to)
    {
        List<Vector2> route = [from];
        Vector2 current = from;
        for (int iteration = 0; iteration < 4; iteration++)
        {
            WorldObstacle? blocker = FirstBlocking(current, to);
            if (blocker is null)
            {
                route.Add(to);
                return route;
            }

            Vector2 corner = ChooseBypassCorner(blocker, current, to);
            route.Add(corner);
            current = corner;
        }

        route.Add(to);
        return route;
    }

    public IReadOnlyList<Vector2> SpliceBypass(IReadOnlyList<Vector2> route)
    {
        if (route.Count == 0) return route;
        List<Vector2> result = [];
        for (int i = 0; i < route.Count; i++)
        {
            if (i == 0)
            {
                result.Add(route[i]);
                continue;
            }

            IReadOnlyList<Vector2> bypass = Bypass(route[i - 1], route[i]);
            for (int j = 1; j < bypass.Count; j++) result.Add(bypass[j]);
        }

        return result;
    }

    private WorldObstacle? FirstBlocking(Vector2 from, Vector2 to)
    {
        float distance = from.DistanceTo(to);
        if (distance < 0.01f) return null;
        int samples = Mathf.Max(2, Mathf.CeilToInt(distance / SampleStep));
        for (int i = 1; i < samples; i++)
        {
            Vector2 point = from.Lerp(to, i / (float)samples);
            foreach (int index in NearbyCells(point))
            {
                if (index < 0 || index >= _obstacles.Count) continue;
                WorldObstacle obstacle = _obstacles[index];
                if (ShouldIgnore(obstacle, from, to)) continue;
                if (obstacle.Footprint.Bounds.Grow(ActorClearance).HasPoint(point)) return obstacle;
            }
        }

        return null;
    }

    private static bool ShouldIgnore(WorldObstacle obstacle, Vector2 from, Vector2 to)
    {
        if (!obstacle.AllowTraversalInside) return false;
        Rect2 bounds = obstacle.Footprint.Bounds;
        if (bounds.Grow(0.08f).HasPoint(from) || bounds.Grow(0.08f).HasPoint(to)) return true;
        return bounds.Grow(-0.08f).HasPoint(from) && bounds.Grow(-0.08f).HasPoint(to);
    }

    private static Vector2 ChooseBypassCorner(WorldObstacle obstacle, Vector2 from, Vector2 to)
    {
        Rect2 bounds = obstacle.Footprint.Bounds.Grow(ActorClearance + 0.18f);
        Vector2[] corners =
        [
            new(bounds.Position.X, bounds.Position.Y),
            new(bounds.End.X, bounds.Position.Y),
            new(bounds.End.X, bounds.End.Y),
            new(bounds.Position.X, bounds.End.Y)
        ];
        return corners
            .OrderBy(corner => from.DistanceSquaredTo(corner) + corner.DistanceSquaredTo(to))
            .First();
    }

    private IEnumerable<int> NearbyCells(Vector2 point)
    {
        Vector2I cell = new(Mathf.FloorToInt(point.X), Mathf.FloorToInt(point.Y));
        for (int y = -1; y <= 1; y++)
        {
            for (int x = -1; x <= 1; x++)
            {
                Vector2I key = cell + new Vector2I(x, y);
                if (_byCell.TryGetValue(key, out List<int>? entries))
                {
                    foreach (int index in entries) yield return index;
                }
            }
        }
    }

    private static IEnumerable<Vector2I> EnumerateCells(Rect2 bounds)
    {
        int startX = Mathf.FloorToInt(bounds.Position.X);
        int startY = Mathf.FloorToInt(bounds.Position.Y);
        int endX = Mathf.CeilToInt(bounds.End.X);
        int endY = Mathf.CeilToInt(bounds.End.Y);
        for (int y = startY; y < endY; y++)
        {
            for (int x = startX; x < endX; x++)
            {
                yield return new Vector2I(x, y);
            }
        }
    }
}
