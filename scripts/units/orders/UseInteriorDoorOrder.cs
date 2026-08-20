#nullable enable

using AshwoodCounty.Buildings.Interiors;
using Godot;

namespace AshwoodCounty.Units.Orders;

public sealed class UseInteriorDoorOrder(InteriorDoorRuntime door) : ISurvivorOrder
{
    private readonly InteriorPathFollower _path = new();
    private bool _arrived;
    private bool _open;
    public SurvivorOrderType Type => SurvivorOrderType.UseDoor;
    public bool IsComplete { get; private set; }
    public void Start(Survivor survivor)
    {
        if (!GodotObject.IsInstanceValid(door)) { IsComplete = true; return; }
        if (!door.IsExterior && !survivor.IsInsideInterior(door.Building)) { IsComplete = true; return; }
        _open = door.State != InteriorDoorState.Open;
        _path.Plan(survivor, door.InteractionPosition);
    }
    public void Tick(Survivor survivor,double delta)
    {
        if(IsComplete||!GodotObject.IsInstanceValid(door)){IsComplete=true;return;}
        if(!_arrived){_arrived=_path.Tick(survivor,delta);return;}
        if ((_open && door.State != InteriorDoorState.Open) || (!_open && door.State == InteriorDoorState.Open)) door.Toggle();
        IsComplete=true;
    }
    public void Cancel(Survivor survivor)=>IsComplete=true;
}
