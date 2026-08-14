using Godot;
using AshwoodCounty.Buildings.Interiors;

namespace AshwoodCounty.Units.Orders;

public sealed class MoveOrder(Vector2 destination) : ISurvivorOrder
{
    private readonly InteriorPathFollower _path = new();
    public SurvivorOrderType Type => SurvivorOrderType.Move;
    public bool IsComplete { get; private set; }
    public Vector2 Destination { get; } = destination;

    public void Start(Survivor survivor)
    {
        _path.Plan(survivor, Destination);
        IsComplete = _path.Tick(survivor, 0);
    }

    public void Tick(Survivor survivor, double delta)
    {
        IsComplete = _path.Tick(survivor, delta);
    }

    public void Cancel(Survivor survivor)
    {
        IsComplete = true;
    }
}
