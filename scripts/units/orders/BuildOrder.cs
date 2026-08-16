using AshwoodCounty.Buildings;
using Godot;

namespace AshwoodCounty.Units.Orders;

public sealed class BuildOrder(ConstructionSite target, Vector2 interactionPosition) : ISurvivorOrder
{
    private enum BuildPhase
    {
        MovingToSite,
        Building
    }

    private readonly ConstructionSite _target = target;
    private readonly Vector2 _interactionPosition = interactionPosition;
    private BuildPhase _phase;
    private ulong _workerId;
    private bool _registered;

    public SurvivorOrderType Type => SurvivorOrderType.Build;
    public bool IsComplete { get; private set; }
    public ConstructionSite Target => _target;

    public void Start(Survivor survivor)
    {
        _workerId = survivor.GetInstanceId();
        if (!GodotObject.IsInstanceValid(_target) || !_target.IsAvailableForBuilding)
        {
            IsComplete = true;
            return;
        }

        _target.BeginBuilding(_workerId);
        _registered = true;
        _phase = BuildPhase.MovingToSite;
    }

    public void Tick(Survivor survivor, double delta)
    {
        if (IsComplete)
        {
            return;
        }

        if (!GodotObject.IsInstanceValid(_target) || !_target.IsAvailableForBuilding)
        {
            Complete();
            return;
        }

        if (_phase == BuildPhase.MovingToSite)
        {
            if (survivor.MoveTowardsGridPositionNavigated(_interactionPosition, delta))
            {
                _phase = BuildPhase.Building;
            }

            return;
        }

        _target.AddConstructionWork(_workerId, (float)delta * survivor.WorkSpeedMultiplier);
        if (!_target.IsAvailableForBuilding)
        {
            Complete();
        }
    }

    public void Cancel(Survivor survivor)
    {
        Complete();
    }

    private void Complete()
    {
        if (_registered && GodotObject.IsInstanceValid(_target))
        {
            _target.EndBuilding(_workerId);
        }

        _registered = false;
        IsComplete = true;
    }
}
