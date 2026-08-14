#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using AshwoodCounty.Units;
using Godot;

namespace AshwoodCounty.Buildings.Interiors;

/// <summary>
/// Local navigation around authored interiors. County travel remains direct;
/// an A* route is generated only when a segment enters a registered building.
/// </summary>
public partial class InteriorNavigationService : Node
{
    public const string GroupName = "interior_navigation";
    private const float Step = .25f;
    private const float ActorClearance = .16f;
    private readonly List<InteriorBuildingRuntime> _buildings = [];

    public override void _Ready() => AddToGroup(GroupName);

    public void Register(InteriorBuildingRuntime building)
    {
        if (!_buildings.Contains(building)) _buildings.Add(building);
    }

    public void Unregister(InteriorBuildingRuntime building) => _buildings.Remove(building);

    public IReadOnlyList<Vector2> PlanRoute(Vector2 start, Vector2 destination)
    {
        InteriorBuildingRuntime? building = _buildings.FirstOrDefault(candidate =>
            candidate.NavigationBounds.HasPoint(start) || candidate.NavigationBounds.HasPoint(destination)
            || SegmentTouchesRect(start, destination, candidate.NavigationBounds));
        if (building is null) return [destination];

        bool startInside = building.Definition.Footprint.Grow(-.1f).HasPoint(start);
        bool destinationInside = building.Definition.Footprint.Grow(-.1f).HasPoint(destination);
        bool destinationIsDoor = building.Definition.Doors.Any(door=>door.Position.DistanceTo(destination)<.08f);
        DoorDefinition? entrance = building.Definition.Doors
            .Where(door => door.Exterior && !door.OutsideApproachPoint.IsZeroApprox() && !door.InsideArrivalPoint.IsZeroApprox())
            .MinBy(door => startInside
                ? destination.DistanceSquaredTo(door.OutsideApproachPoint)
                : start.DistanceSquaredTo(door.OutsideApproachPoint));

        IReadOnlyList<Vector2> route;
        if(destinationIsDoor)
        {
            route=PlanLocal(building,start,destination);
        }
        else if (!startInside && destinationInside && entrance is not null)
        {
            List<Vector2> outside = [.. PlanLocal(building,start,entrance.OutsideApproachPoint)];
            List<Vector2> local = [.. PlanLocal(building,entrance.InsideArrivalPoint,destination)];
            route = [..outside,entrance.Position,entrance.InsideArrivalPoint,.. local.Skip(1)];
        }
        else if (startInside && !destinationInside && entrance is not null)
        {
            List<Vector2> local = [.. PlanLocal(building,start,entrance.InsideArrivalPoint)];
            List<Vector2> outside = [.. PlanLocal(building,entrance.OutsideApproachPoint,destination)];
            route = [.. local,entrance.Position,..outside];
        }
        else
        {
            route = PlanLocal(building,start,destination);
        }

        if(System.Environment.GetEnvironmentVariable("ASHWOOD_VALIDATE_INTERIOR")=="1")
            GD.Print($"INTERIOR_ROUTE: {start} -> {destination} via {string.Join(" | ",route)}");
        return route;
    }

    private static IReadOnlyList<Vector2> PlanLocal(InteriorBuildingRuntime building, Vector2 start, Vector2 destination)
        => PlanLocal(building.NavigationBounds,building.NavigationBlockers,start,destination);

    public static IReadOnlyList<Vector2> PlanDefinition(InteriorBuildingDefinition definition,Vector2 start,Vector2 destination)
        => PlanLocal(definition.Footprint.Grow(.35f),InteriorBuildingRuntime.BuildNavigationBlockers(definition),start,destination);

    public static bool CanReach(InteriorBuildingDefinition definition,Vector2 start,Vector2 destination)
    {
        IReadOnlyList<Vector2> route=PlanDefinition(definition,start,destination);
        return route.Count>0&&route[^1].DistanceTo(destination)<.35f;
    }

    private static IReadOnlyList<Vector2> PlanLocal(Rect2 navigationBounds,IReadOnlyList<Rect2> blockers,Vector2 start,Vector2 destination)
    {
        Rect2 bounds = navigationBounds.Grow(1.0f);
        Vector2I gridSize = new(
            Mathf.CeilToInt(bounds.Size.X / Step) + 1,
            Mathf.CeilToInt(bounds.Size.Y / Step) + 1);
        Vector2I startCell = FindNearestFree(ToCell(start, bounds, gridSize), bounds, gridSize, blockers);
        Vector2I goalCell = FindNearestFree(ToCell(destination, bounds, gridSize), bounds, gridSize, blockers);

        PriorityQueue<Vector2I, float> frontier = new();
        Dictionary<Vector2I, Vector2I> cameFrom = [];
        Dictionary<Vector2I, float> cost = new() { [startCell] = 0 };
        frontier.Enqueue(startCell, 0);
        Vector2I[] directions =
        [
            Vector2I.Right, Vector2I.Left, Vector2I.Up, Vector2I.Down,
            new(1,1), new(1,-1), new(-1,1), new(-1,-1)
        ];

        while (frontier.Count > 0)
        {
            Vector2I current = frontier.Dequeue();
            if (current == goalCell) break;
            foreach (Vector2I direction in directions)
            {
                Vector2I next = current + direction;
                if (!InGrid(next, gridSize) || !IsFree(ToWorld(next, bounds), blockers)) continue;
                if (direction.X != 0 && direction.Y != 0
                    && (!IsFree(ToWorld(current + new Vector2I(direction.X, 0), bounds), blockers)
                        || !IsFree(ToWorld(current + new Vector2I(0, direction.Y), bounds), blockers)))
                    continue;
                float newCost = cost[current] + (direction.X == 0 || direction.Y == 0 ? 1f : 1.4142f);
                if (cost.TryGetValue(next, out float previous) && previous <= newCost) continue;
                cost[next] = newCost;
                cameFrom[next] = current;
                float heuristic = Mathf.Abs(goalCell.X - next.X) + Mathf.Abs(goalCell.Y - next.Y);
                frontier.Enqueue(next, newCost + heuristic);
            }
        }

        if (startCell != goalCell && !cameFrom.ContainsKey(goalCell)) return [start];
        List<Vector2I> cells = [goalCell];
        while (cells[^1] != startCell) cells.Add(cameFrom[cells[^1]]);
        cells.Reverse();
        List<Vector2> raw = cells.Select(cell => ToWorld(cell, bounds)).ToList();
        raw[0] = start;
        if (IsFree(destination, blockers)) raw[^1] = destination;
        List<Vector2> simplified=Simplify(raw, blockers);
        return simplified;
    }

    public bool PrepareTraversal(Vector2 from, Vector2 to, Survivor survivor)
    {
        foreach (InteriorBuildingRuntime building in _buildings)
        {
            if (building.NavigationBounds.Grow(.5f).HasPoint(from)
                || building.NavigationBounds.Grow(.5f).HasPoint(to)
                || SegmentTouchesRect(from, to, building.NavigationBounds))
            {
                if (!building.OpenDoorsAlong(from, to, survivor)) return false;
            }
        }
        return true;
    }

    private static List<Vector2> Simplify(List<Vector2> raw, IReadOnlyList<Rect2> blockers)
    {
        if (raw.Count <= 2) return raw;
        List<Vector2> result = [raw[0]];
        int anchor = 0;
        while (anchor < raw.Count - 1)
        {
            int next = raw.Count - 1;
            while (next > anchor + 1 && !HasLineOfSight(raw[anchor], raw[next], blockers)) next--;
            result.Add(raw[next]);
            anchor = next;
        }
        return result;
    }

    private static bool HasLineOfSight(Vector2 start, Vector2 end, IReadOnlyList<Rect2> blockers)
    {
        float distance = start.DistanceTo(end);
        int samples = Mathf.Max(1, Mathf.CeilToInt(distance / (Step * .45f)));
        for (int i = 1; i < samples; i++)
            if (!IsFree(start.Lerp(end, i / (float)samples), blockers)) return false;
        return true;
    }

    private static Vector2I FindNearestFree(Vector2I origin, Rect2 bounds, Vector2I gridSize, IReadOnlyList<Rect2> blockers)
    {
        if (InGrid(origin, gridSize) && IsFree(ToWorld(origin, bounds), blockers)) return origin;
        for (int radius = 1; radius < Mathf.Max(gridSize.X, gridSize.Y); radius++)
        {
            for (int y = -radius; y <= radius; y++)
            for (int x = -radius; x <= radius; x++)
            {
                if (Mathf.Abs(x) != radius && Mathf.Abs(y) != radius) continue;
                Vector2I candidate = origin + new Vector2I(x, y);
                if (InGrid(candidate, gridSize) && IsFree(ToWorld(candidate, bounds), blockers)) return candidate;
            }
        }
        return origin;
    }

    private static bool IsFree(Vector2 point, IReadOnlyList<Rect2> blockers)
    {
        foreach (Rect2 blocker in blockers)
            if (blocker.Grow(ActorClearance).HasPoint(point)) return false;
        return true;
    }

    private static Vector2I ToCell(Vector2 point, Rect2 bounds, Vector2I gridSize) => new(
        Mathf.Clamp(Mathf.RoundToInt((point.X - bounds.Position.X) / Step), 0, gridSize.X - 1),
        Mathf.Clamp(Mathf.RoundToInt((point.Y - bounds.Position.Y) / Step), 0, gridSize.Y - 1));
    private static Vector2 ToWorld(Vector2I cell, Rect2 bounds) => bounds.Position + new Vector2(cell.X, cell.Y) * Step;
    private static bool InGrid(Vector2I cell, Vector2I size) => cell.X >= 0 && cell.Y >= 0 && cell.X < size.X && cell.Y < size.Y;

    private static bool SegmentTouchesRect(Vector2 a, Vector2 b, Rect2 rect)
    {
        if (rect.HasPoint(a) || rect.HasPoint(b)) return true;
        int samples = Mathf.Max(2, Mathf.CeilToInt(a.DistanceTo(b) / .5f));
        for (int i = 1; i < samples; i++) if (rect.HasPoint(a.Lerp(b, i / (float)samples))) return true;
        return false;
    }
}

/// <summary>Small reusable route cursor shared by movement and interaction orders.</summary>
public sealed class InteriorPathFollower
{
    private IReadOnlyList<Vector2> _route = [];
    private int _index;

    public void Plan(Survivor survivor, Vector2 destination)
    {
        InteriorNavigationService? navigation = survivor.GetTree().GetFirstNodeInGroup(InteriorNavigationService.GroupName) as InteriorNavigationService;
        _route = navigation?.PlanRoute(survivor.SimulationPosition, destination) ?? [destination];
        _index = _route.Count > 1 && survivor.SimulationPosition.DistanceTo(_route[0]) <= survivor.ArrivalThreshold ? 1 : 0;
    }

    public bool Tick(Survivor survivor, double delta)
    {
        if (_route.Count == 0 || _index >= _route.Count) return true;
        Vector2 target = _route[_index];
        InteriorNavigationService? navigation = survivor.GetTree().GetFirstNodeInGroup(InteriorNavigationService.GroupName) as InteriorNavigationService;
        if (navigation is not null && !navigation.PrepareTraversal(survivor.SimulationPosition, target, survivor))
        {
            survivor.StopMovement();
            return false;
        }
        if (!survivor.MoveTowardsGridPosition(target, delta)) return false;
        _index++;
        return _index >= _route.Count;
    }
}
