#nullable enable

using AshwoodCounty.Buildings.Interiors;
using Godot;

namespace AshwoodCounty.Units.Orders;

/// <summary>
/// Walks a survivor to a building's authored exterior entrance and only then
/// steps inside through it. The interior activation remains driven by the
/// building runtime, which fades the exterior once the survivor is inside the
/// footprint.
/// </summary>
public sealed class EnterBuildingOrder(InteriorBuildingRuntime building) : ISurvivorOrder
{
    private readonly InteriorPathFollower _path = new();
    private bool _approaching = true;

    public SurvivorOrderType Type => SurvivorOrderType.EnterBuilding;
    public bool IsComplete { get; private set; }

    public void Start(Survivor survivor)
    {
        if (building is null || !GodotObject.IsInstanceValid(building) || building.ExteriorEntrance is not DoorDefinition entrance)
        {
            IsComplete = true;
            return;
        }

        if (survivor.IsInsideInterior(building))
        {
            IsComplete = true;
            return;
        }

        _path.Plan(survivor, entrance.OutsideApproachPoint);
        if (_path.Unreachable) IsComplete = true;
    }

    public void Tick(Survivor survivor, double delta)
    {
        if (IsComplete || building is null || !GodotObject.IsInstanceValid(building))
        {
            IsComplete = true;
            return;
        }

        if (_approaching)
        {
            if (_path.Blocked) { IsComplete = true; return; }
            if (!_path.Tick(survivor, delta)) return;
            _approaching = false;
            if (building.ExteriorEntrance is not DoorDefinition entrance)
            {
                IsComplete = true;
                return;
            }

            _path.Plan(survivor, entrance.InsideArrivalPoint);
            if (_path.Unreachable) IsComplete = true;
            return;
        }

        if (_path.Blocked) { IsComplete = true; return; }
        if (_path.Tick(survivor, delta)) IsComplete = true;
    }

    public void Cancel(Survivor survivor) => IsComplete = true;
}
