#nullable enable

using System.Collections.Generic;
using AshwoodCounty.Items;
using AshwoodCounty.Resources;
using AshwoodCounty.UI;
using Godot;

namespace AshwoodCounty.Units.Orders;

/// <summary>
/// Collects a designated loose drop and carries it to settlement item storage.
/// Uses the existing item catalog / survivor inventory / storage pipeline; the
/// storage converts resource-relationship items into the bulk settlement
/// economy exactly like every other deposit path.
/// </summary>
public sealed class HaulOrder(
    HaulableDrop target,
    Stockpile stockpile,
    SettlementItemStorage itemStorage,
    Vector2 interactionPosition,
    Vector2 deliveryPosition) : ISurvivorOrder
{
    private enum Phase { MovingToDrop, MovingToStorage }

    private readonly HaulableDrop _target = target;
    private readonly Stockpile _stockpile = stockpile;
    private readonly SettlementItemStorage _itemStorage = itemStorage;
    private readonly Vector2 _interactionPosition = interactionPosition;
    private readonly Vector2 _deliveryPosition = deliveryPosition;
    private readonly List<ItemStack> _carried = [];
    private Phase _phase;
    private ulong _workerId;
    private bool _claimed;

    public SurvivorOrderType Type => SurvivorOrderType.Haul;
    public bool IsComplete { get; private set; }

    public void Start(Survivor survivor)
    {
        _workerId = survivor.GetInstanceId();
        if (!GodotObject.IsInstanceValid(_target) || !GodotObject.IsInstanceValid(_stockpile) || _itemStorage is null)
        {
            Notify(survivor, "Unavailable");
            IsComplete = true;
            return;
        }

        if (!_target.HasItems)
        {
            IsComplete = true;
            return;
        }

        if (!_target.TryClaim(_workerId))
        {
            Notify(survivor, "Already being hauled");
            IsComplete = true;
            return;
        }

        _claimed = true;
        _phase = Phase.MovingToDrop;
    }

    public void Tick(Survivor survivor, double delta)
    {
        if (IsComplete || !GodotObject.IsInstanceValid(_target) || !GodotObject.IsInstanceValid(_stockpile))
        {
            Complete();
            return;
        }

        if (_phase == Phase.MovingToDrop)
        {
            if (!_target.HasItems)
            {
                Complete();
                return;
            }

            if (!survivor.MoveTowardsGridPositionNavigated(_interactionPosition, delta)) return;
            PickUp(survivor);
            return;
        }

        if (!survivor.MoveTowardsGridPositionNavigated(_deliveryPosition, delta)) return;
        Deliver(survivor);
        Complete();
    }

    public void Cancel(Survivor survivor) => Complete();

    private void PickUp(Survivor survivor)
    {
        _carried.Clear();
        foreach (ItemStack stack in _target.Stacks)
        {
            _carried.Add(stack);
        }

        int taken = _target.TakeAvailable(survivor.Inventory);
        ReleaseClaimIfHeld();
        if (taken <= 0)
        {
            Notify(survivor, "No carry space");
            IsComplete = true;
            return;
        }

        survivor.SetWorkLabel("Hauling to storage");
        _phase = Phase.MovingToStorage;
    }

    private void Deliver(Survivor survivor)
    {
        foreach (ItemStack stack in _carried)
        {
            int held = survivor.Inventory.GetQuantity(stack.ItemId);
            int amount = Mathf.Min(held, stack.Quantity);
            if (amount <= 0 || !survivor.Inventory.TryRemove(stack.ItemId, amount)) continue;
            _itemStorage.Deposit(stack.ItemId, amount);
        }

        _carried.Clear();
    }

    private void ReleaseClaimIfHeld()
    {
        if (_claimed && GodotObject.IsInstanceValid(_target))
        {
            _target.ReleaseClaim(_workerId);
        }

        _claimed = false;
    }

    private void Complete()
    {
        ReleaseClaimIfHeld();
        IsComplete = true;
    }

    private void Notify(Survivor survivor, string message)
    {
        if (!GodotObject.IsInstanceValid(_target)) return;
        (survivor.GetTree().GetFirstNodeInGroup(GameHud.GroupName) as GameHud)?.Notify($"{_target.DisplayName.ToUpperInvariant()}\n{message}");
    }
}
