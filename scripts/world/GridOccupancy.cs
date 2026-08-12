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
    Vector2I OccupancyOrigin { get; }
    Vector2I OccupancyFootprint { get; }
}

public partial class GridOccupancy : Node
{
    public const string OccupantGroup = "grid_occupants";

    private readonly Dictionary<Vector2I, GodotObject> _occupantsByCell = [];
    private readonly Dictionary<ulong, List<Vector2I>> _cellsByOccupant = [];

    public override void _Ready()
    {
        Callable.From(RegisterInitialOccupants).CallDeferred();
    }

    public PlacementFailure Validate(Vector2I origin, Vector2I footprint)
    {
        if (origin.X < 0 || origin.Y < 0 || footprint.X <= 0 || footprint.Y <= 0
            || origin.X + footprint.X > IsometricWorld.MapWidth
            || origin.Y + footprint.Y > IsometricWorld.MapHeight)
        {
            return PlacementFailure.OutsideMap;
        }

        foreach (Vector2I cell in EnumerateCells(origin, footprint))
        {
            if (_occupantsByCell.ContainsKey(cell))
            {
                return PlacementFailure.Occupied;
            }
        }

        return PlacementFailure.None;
    }

    public bool TryOccupy(GodotObject occupant, Vector2I origin, Vector2I footprint)
    {
        if (occupant is null || Validate(origin, footprint) != PlacementFailure.None)
        {
            return false;
        }

        List<Vector2I> cells = [.. EnumerateCells(origin, footprint)];
        _cellsByOccupant[occupant.GetInstanceId()] = cells;
        foreach (Vector2I cell in cells)
        {
            _occupantsByCell[cell] = occupant;
        }

        return true;
    }

    public bool Release(GodotObject occupant)
    {
        if (occupant is null || !_cellsByOccupant.Remove(occupant.GetInstanceId(), out List<Vector2I> cells))
        {
            return false;
        }

        foreach (Vector2I cell in cells)
        {
            _occupantsByCell.Remove(cell);
        }

        return true;
    }

    public bool Transfer(GodotObject previousOccupant, GodotObject newOccupant)
    {
        if (previousOccupant is null || newOccupant is null
            || !_cellsByOccupant.Remove(previousOccupant.GetInstanceId(), out List<Vector2I> cells))
        {
            return false;
        }

        _cellsByOccupant[newOccupant.GetInstanceId()] = cells;
        foreach (Vector2I cell in cells)
        {
            _occupantsByCell[cell] = newOccupant;
        }

        return true;
    }

    public bool IsOccupied(Vector2I cell)
    {
        return _occupantsByCell.ContainsKey(cell);
    }

    private void RegisterInitialOccupants()
    {
        foreach (Node node in GetTree().GetNodesInGroup(OccupantGroup))
        {
            if (node is GodotObject occupant && node is IGridOccupant gridOccupant
                && !_cellsByOccupant.ContainsKey(occupant.GetInstanceId()))
            {
                if (!TryOccupy(occupant, gridOccupant.OccupancyOrigin, gridOccupant.OccupancyFootprint))
                {
                    GD.PushWarning($"Could not register grid occupancy for {node.GetPath()}.");
                }
            }
        }
    }

    private static IEnumerable<Vector2I> EnumerateCells(Vector2I origin, Vector2I footprint)
    {
        for (int y = 0; y < footprint.Y; y++)
        {
            for (int x = 0; x < footprint.X; x++)
            {
                yield return origin + new Vector2I(x, y);
            }
        }
    }
}
