#nullable enable

using AshwoodCounty.Buildings.Interiors;
using Godot;

namespace AshwoodCounty.Units.Orders;

public sealed class UseInteriorBedOrder(InteriorBedRuntime bed, float restoredEnergy = 94f) : ISurvivorOrder
{
    private readonly InteriorPathFollower _path = new();
    private ulong _survivorId;
    private bool _reserved;
    private bool _arrived;

    public SurvivorOrderType Type => SurvivorOrderType.UseBed;
    public bool IsComplete { get; private set; }

    public void Start(Survivor survivor)
    {
        _survivorId = survivor.GetInstanceId();
        if (!GodotObject.IsInstanceValid(bed) || !bed.TryReserve(_survivorId)) { IsComplete = true; return; }
        if (!survivor.IsInsideInterior(bed.Building))
        {
            bed.Release(_survivorId);
            IsComplete = true;
            return;
        }
        _reserved = true;
        _path.Plan(survivor, bed.InteractionPosition);
    }

    public void Tick(Survivor survivor, double delta)
    {
        if (IsComplete || !GodotObject.IsInstanceValid(bed)) { Complete(); return; }
        if (!_arrived) { _arrived = _path.Tick(survivor, delta); return; }
        survivor.StopMovement();
        survivor.Energy = Mathf.Min(100f, survivor.Energy + 18f * (float)delta);
        bed.MarkUsed();
        if (survivor.Energy >= restoredEnergy) Complete();
    }

    public void Cancel(Survivor survivor) => Complete();
    private void Complete()
    {
        if (_reserved && GodotObject.IsInstanceValid(bed)) bed.Release(_survivorId);
        _reserved = false; IsComplete = true;
    }
}
