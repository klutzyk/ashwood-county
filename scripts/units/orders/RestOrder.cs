using AshwoodCounty.Buildings;
using Godot;

namespace AshwoodCounty.Units.Orders;

public sealed class RestOrder(CompletedBuilding shelter, float restoredEnergy = 92f) : ISurvivorOrder
{
    private Vector2 _restPosition;
    private ulong _survivorId;
    private bool _reserved;
    private bool _arrived;

    public SurvivorOrderType Type => SurvivorOrderType.Rest;
    public bool IsComplete { get; private set; }

    public void Start(Survivor survivor)
    {
        _survivorId = survivor.GetInstanceId();
        if (!GodotObject.IsInstanceValid(shelter) || !shelter.TryReserveRestSlot(_survivorId, out _restPosition))
        {
            IsComplete = true;
            return;
        }

        _reserved = true;
    }

    public void Tick(Survivor survivor, double delta)
    {
        if (IsComplete) return;
        if (!GodotObject.IsInstanceValid(shelter)) { Complete(); return; }
        if (!_arrived) { _arrived = survivor.MoveTowardsGridPosition(_restPosition, delta); return; }

        survivor.StopMovement();
        survivor.Energy = Mathf.Min(100f, survivor.Energy + 14f * (float)delta);
        if (survivor.Energy >= restoredEnergy) Complete();
    }

    public void Cancel(Survivor survivor) => Complete();

    private void Complete()
    {
        if (_reserved && GodotObject.IsInstanceValid(shelter)) shelter.ReleaseRestSlot(_survivorId);
        _reserved = false;
        IsComplete = true;
    }
}
