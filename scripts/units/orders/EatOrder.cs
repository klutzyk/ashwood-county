using AshwoodCounty.Resources;
using Godot;

namespace AshwoodCounty.Units.Orders;

public sealed class EatOrder(SettlementInventory inventory, Stockpile stockpile, Vector2 interactionPosition) : ISurvivorOrder
{
    public SurvivorOrderType Type => SurvivorOrderType.Eat;
    public bool IsComplete { get; private set; }
    public void Start(Survivor survivor)
    {
        if (!GodotObject.IsInstanceValid(stockpile)) IsComplete = true;
    }
    public void Tick(Survivor survivor, double delta)
    {
        if (IsComplete || !survivor.MoveTowardsGridPositionNavigated(interactionPosition, delta)) return;
        if (inventory.TrySpend(ResourceType.Food, 1)) survivor.EatMeal();
        IsComplete = true;
    }
    public void Cancel(Survivor survivor) => IsComplete = true;
}
