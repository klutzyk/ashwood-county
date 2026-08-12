using Godot;

namespace AshwoodCounty.Units.Orders;

public sealed class MoveOrder(Vector2 destination) : ISurvivorOrder
{
    public SurvivorOrderType Type => SurvivorOrderType.Move;
    public bool IsComplete { get; private set; }
    public Vector2 Destination { get; } = destination;

    public void Start(Survivor survivor)
    {
        IsComplete = survivor.MoveTowardsGridPosition(Destination, 0);
    }

    public void Tick(Survivor survivor, double delta)
    {
        IsComplete = survivor.MoveTowardsGridPosition(Destination, delta);
    }

    public void Cancel(Survivor survivor)
    {
        IsComplete = true;
    }
}
