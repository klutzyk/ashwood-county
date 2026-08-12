using System.Collections.Generic;
using Godot;

namespace AshwoodCounty.World;

public enum PlacementFailure
{
    None,
    OutsideMap,
    Occupied
}

public interface IGridOccupant
{
    WorldFootprint OccupancyFootprint { get; }
}

public partial class GridOccupancy : Node
{
    public const string OccupantGroup = "grid_occupants";

    private sealed record OccupancyEntry(GodotObject Occupant, WorldFootprint Footprint, List<Vector2I> Cells);

    private readonly Dictionary<Vector2I, HashSet<ulong>> _occupantsByCell = [];
    private readonly Dictionary<ulong, OccupancyEntry> _occupants = [];

    public override void _Ready()
    {
        Callable.From(RegisterInitialOccupants).CallDeferred();
    }

    public PlacementFailure Validate(WorldFootprint footprint)
    {
        Rect2 bounds = footprint.Bounds;
        if (!footprint.IsValid || bounds.Position.X < 0 || bounds.Position.Y < 0
            || bounds.End.X > IsometricWorld.MapWidth || bounds.End.Y > IsometricWorld.MapHeight)
        {
            return PlacementFailure.OutsideMap;
        }

        HashSet<ulong> candidates = [];
        foreach (Vector2I cell in EnumerateCells(bounds))
        {
            if (_occupantsByCell.TryGetValue(cell, out HashSet<ulong> occupants))
            {
                candidates.UnionWith(occupants);
            }
        }

        foreach (ulong candidateId in candidates)
        {
            if (_occupants.TryGetValue(candidateId, out OccupancyEntry entry)
                && footprint.Overlaps(entry.Footprint))
            {
                return PlacementFailure.Occupied;
            }
        }

        return PlacementFailure.None;
    }

    public bool TryOccupy(GodotObject occupant, WorldFootprint footprint)
    {
        if (occupant is null || _occupants.ContainsKey(occupant.GetInstanceId())
            || Validate(footprint) != PlacementFailure.None)
        {
            return false;
        }

        ulong occupantId = occupant.GetInstanceId();
        List<Vector2I> cells = [.. EnumerateCells(footprint.Bounds)];
        _occupants[occupantId] = new OccupancyEntry(occupant, footprint, cells);
        foreach (Vector2I cell in cells)
        {
            if (!_occupantsByCell.TryGetValue(cell, out HashSet<ulong> occupants))
            {
                occupants = [];
                _occupantsByCell[cell] = occupants;
            }

            occupants.Add(occupantId);
        }

        return true;
    }

    public bool Release(GodotObject occupant)
    {
        if (occupant is null || !_occupants.Remove(occupant.GetInstanceId(), out OccupancyEntry entry))
        {
            return false;
        }

        foreach (Vector2I cell in entry.Cells)
        {
            RemoveCellOccupant(cell, occupant.GetInstanceId());
        }

        return true;
    }

    public bool Transfer(GodotObject previousOccupant, GodotObject newOccupant)
    {
        if (previousOccupant is null || newOccupant is null
            || _occupants.ContainsKey(newOccupant.GetInstanceId())
            || !_occupants.Remove(previousOccupant.GetInstanceId(), out OccupancyEntry entry))
        {
            return false;
        }

        ulong previousId = previousOccupant.GetInstanceId();
        ulong newId = newOccupant.GetInstanceId();
        _occupants[newId] = new OccupancyEntry(newOccupant, entry.Footprint, entry.Cells);
        foreach (Vector2I cell in entry.Cells)
        {
            RemoveCellOccupant(cell, previousId);
            if (!_occupantsByCell.TryGetValue(cell, out HashSet<ulong> occupants))
            {
                occupants = [];
                _occupantsByCell[cell] = occupants;
            }

            occupants.Add(newId);
        }

        return true;
    }

    public bool IsOccupied(Vector2I cell)
    {
        return _occupantsByCell.TryGetValue(cell, out HashSet<ulong> occupants) && occupants.Count > 0;
    }

    private void RegisterInitialOccupants()
    {
        foreach (Node node in GetTree().GetNodesInGroup(OccupantGroup))
        {
            if (node is GodotObject occupant && node is IGridOccupant gridOccupant
                && !_occupants.ContainsKey(occupant.GetInstanceId()))
            {
                if (!TryOccupy(occupant, gridOccupant.OccupancyFootprint))
                {
                    GD.PushWarning($"Could not register grid occupancy for {node.GetPath()}.");
                }
            }
        }
    }

    private void RemoveCellOccupant(Vector2I cell, ulong occupantId)
    {
        if (!_occupantsByCell.TryGetValue(cell, out HashSet<ulong> occupants))
        {
            return;
        }

        occupants.Remove(occupantId);
        if (occupants.Count == 0)
        {
            _occupantsByCell.Remove(cell);
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
