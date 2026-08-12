using AshwoodCounty.Resources;
using Godot;

namespace AshwoodCounty.Units.Orders;

public sealed class HarvestResourceOrder(
    HarvestableResource target,
    Stockpile stockpile,
    Vector2 interactionPosition,
    Vector2 deliveryPosition) : ISurvivorOrder
{
    private enum HarvestPhase
    {
        MovingToResource,
        Harvesting,
        Delivering
    }

    private readonly HarvestableResource _target = target;
    private readonly Stockpile _stockpile = stockpile;
    private readonly Vector2 _interactionPosition = interactionPosition;
    private readonly Vector2 _deliveryPosition = deliveryPosition;
    private HarvestPhase _phase;
    private float _harvestElapsed;
    private ulong _workerId;
    private bool _registeredWithTarget;

    public SurvivorOrderType Type => SurvivorOrderType.HarvestResource;
    public bool IsComplete { get; private set; }

    public void Start(Survivor survivor)
    {
        _workerId = survivor.GetInstanceId();
        if (!GodotObject.IsInstanceValid(_target) || !GodotObject.IsInstanceValid(_stockpile))
        {
            IsComplete = true;
            return;
        }

        _target.BeginTargeting(_workerId);
        _registeredWithTarget = true;
        _phase = survivor.CarriedAmount > 0 ? HarvestPhase.Delivering : HarvestPhase.MovingToResource;
    }

    public void Tick(Survivor survivor, double delta)
    {
        if (IsComplete)
        {
            return;
        }

        if (!GodotObject.IsInstanceValid(_target) || !GodotObject.IsInstanceValid(_stockpile))
        {
            Complete();
            return;
        }

        switch (_phase)
        {
            case HarvestPhase.MovingToResource:
                MoveToResource(survivor, delta);
                break;
            case HarvestPhase.Harvesting:
                Harvest(survivor, delta);
                break;
            case HarvestPhase.Delivering:
                Deliver(survivor, delta);
                break;
        }
    }

    public void Cancel(Survivor survivor)
    {
        Complete();
    }

    private void MoveToResource(Survivor survivor, double delta)
    {
        if (!_target.IsHarvestable)
        {
            Complete();
            return;
        }

        if (survivor.MoveTowardsGridPosition(_interactionPosition, delta))
        {
            _harvestElapsed = 0;
            _phase = HarvestPhase.Harvesting;
        }
    }

    private void Harvest(Survivor survivor, double delta)
    {
        if (!_target.IsHarvestable)
        {
            Complete();
            return;
        }

        _harvestElapsed += (float)delta * survivor.WorkSpeedMultiplier;
        _target.ReportHarvestProgress(_workerId, _harvestElapsed / _target.HarvestDuration);
        if (_harvestElapsed < _target.HarvestDuration)
        {
            return;
        }

        int capacity = survivor.GetRemainingCarryCapacity(_target.ResourceType);
        int harvested = _target.TryHarvest(_target.ResourceType, capacity);
        _target.ReportHarvestProgress(_workerId, 0);
        if (harvested <= 0 || !survivor.TryAddCarriedResource(_target.ResourceType, harvested))
        {
            Complete();
            return;
        }

        _phase = HarvestPhase.Delivering;
    }

    private void Deliver(Survivor survivor, double delta)
    {
        if (!survivor.MoveTowardsGridPosition(_deliveryPosition, delta))
        {
            return;
        }

        int delivered = survivor.RemoveCarriedResource();
        if (delivered > 0)
        {
            _stockpile.Deposit(survivor.LastCarriedResourceType, delivered);
        }

        if (_target.IsHarvestable)
        {
            _phase = HarvestPhase.MovingToResource;
        }
        else
        {
            Complete();
        }
    }

    private void Complete()
    {
        if (_registeredWithTarget && GodotObject.IsInstanceValid(_target))
        {
            _target.EndTargeting(_workerId);
        }

        _registeredWithTarget = false;
        IsComplete = true;
    }
}
