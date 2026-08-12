using System.Linq;
using AshwoodCounty.Buildings;
using AshwoodCounty.Jobs;
using AshwoodCounty.Resources;
using AshwoodCounty.Units.Orders;
using AshwoodCounty.World;
using Godot;
using AshwoodCounty.Threats;

namespace AshwoodCounty.Units;

[Tool]
public partial class Survivor : Node2D
{
    public const string GroupName = "survivors";

    private Vector2 _simulationPosition;

    [Export]
    public Vector2 SimulationPosition
    {
        get => _simulationPosition;
        set
        {
            _simulationPosition = value;
            if (Engine.IsEditorHint())
            {
                UpdateRenderedPosition();
            }
        }
    }

    [Export] public float MovementSpeed { get; set; } = 2.5f;
    [Export] public float ArrivalThreshold { get; set; } = 0.05f;
    [Export] public int CarryCapacityWood { get; set; } = 10;
    [Export] public int CarryCapacityFood { get; set; } = 6;
    [Export] public float Hunger { get; set; } = 82.0f;
    [Export] public float HungerDepletionPerSecond { get; set; } = 0.22f;
    [Export] public float HungryThreshold { get; set; } = 55.0f;
    [Export] public float CriticalHungerThreshold { get; set; } = 25.0f;
    [Export] public float MealRestoration { get; set; } = 48.0f;
    [Export] public float MaxHealth { get; set; } = 100.0f;
    [Export] public float MeleeDamage { get; set; } = 18.0f;
    [Export] public float AutoDefenseRange { get; set; } = 2.2f;

    private CanvasItem _selectionIndicator = null!;
    private CanvasItem _visual = null!;
    private Vector2 _destination;
    private ISurvivorOrder _currentOrder = null!;
    private bool _isAutonomousOrder;
    private float _health;
    private bool _dead;
    private float _defenseScanElapsed;

    public bool IsSelected { get; private set; }
    public bool HasMoveOrder => CurrentOrderType == SurvivorOrderType.Move;
    public Vector2 Destination => _destination;
    public SurvivorOrderType CurrentOrderType => _currentOrder?.Type ?? SurvivorOrderType.None;
    public int CarriedAmount { get; private set; }
    public ResourceType CarriedResourceType { get; private set; } = ResourceType.Wood;
    public ResourceType LastCarriedResourceType { get; private set; } = ResourceType.Wood;
    public Vector2 MovementVector { get; private set; }
    public bool IsMoving => MovementVector.LengthSquared() > 0.000001f;
    public bool IsAutonomousOrder => _isAutonomousOrder && _currentOrder is not null;
    public bool IsAvailableForAutonomousWork => _currentOrder is null;
    public bool IsAlive => !_dead;
    public float Health => _health;
    public bool NeedsMeal => Hunger <= HungryThreshold;
    public bool IsCriticallyHungry => Hunger <= CriticalHungerThreshold;
    public float WorkSpeedMultiplier => IsCriticallyHungry ? .65f : NeedsMeal ? .85f : 1.0f;
    public string Activity => CurrentOrderType switch
    {
        SurvivorOrderType.Move => "Moving",
        SurvivorOrderType.HarvestResource => CarriedAmount > 0 ? "Carrying / Delivering" : "Chopping",
        SurvivorOrderType.Build => "Building",
        SurvivorOrderType.Eat => "Eating",
        SurvivorOrderType.AttackZombie => "Fighting",
        _ when _dead => "Dead",
        _ => NeedsMeal ? "Idle • Hungry" : "Idle"
    };

    public override void _Ready()
    {
        if (Engine.IsEditorHint())
        {
            SetPhysicsProcess(false);
            UpdateRenderedPosition();
            return;
        }

        AddToGroup(GroupName);
        _health = MaxHealth;
        _selectionIndicator = GetNode<CanvasItem>("SelectionIndicator");
        _visual = GetNode<CanvasItem>("Visual");
        SetSelected(false);
        UpdateRenderedPosition();
    }

    public override void _PhysicsProcess(double delta)
    {
        if (Engine.IsEditorHint())
        {
            return;
        }

        Hunger = Mathf.Max(0, Hunger - HungerDepletionPerSecond * (float)delta);
        if(_dead)return;
        _defenseScanElapsed-=(float)delta;
        if(_defenseScanElapsed<=0){_defenseScanElapsed=.3f;TryAutoDefend();}
        if (_currentOrder is null)
        {
            return;
        }

        _currentOrder.Tick(this, delta);
        if (_currentOrder.IsComplete)
        {
            ReleaseAutonomousClaim();
            _currentOrder = null!;
        }
    }

    public void SetSelected(bool selected)
    {
        IsSelected = selected;
        if (IsInstanceValid(_selectionIndicator))
        {
            _selectionIndicator.Visible = selected;
        }
    }

    public void IssueMoveOrder(Vector2 destination)
    {
        AssignOrder(new MoveOrder(destination), false);
    }

    public void IssueHarvestOrder(HarvestableResource target, Stockpile stockpile, Vector2 interactionPosition, Vector2 deliveryPosition)
    {
        AssignOrder(new HarvestResourceOrder(target, stockpile, interactionPosition, deliveryPosition), false);
    }

    public void IssueBuildOrder(ConstructionSite target, Vector2 interactionPosition)
    {
        AssignOrder(new BuildOrder(target, interactionPosition), false);
    }
    public void IssueAttackOrder(Zombie target) => AssignOrder(new AttackZombieOrder(target), false);
    public void IssueAutonomousAttackOrder(Zombie target) => AssignOrder(new AttackZombieOrder(target), true);
    public void TakeDamage(float amount,Zombie attacker)
    {
        if(_dead||amount<=0)return;_health=Mathf.Max(0,_health-amount);RefreshVisual();if(_health<=0){_dead=true;_currentOrder?.Cancel(this);_currentOrder=null!;MovementVector=Vector2.Zero;SetSelected(false);RemoveFromGroup(GroupName);}else if(_currentOrder is null||_isAutonomousOrder)IssueAutonomousAttackOrder(attacker);
    }
    public void StopMovement(){MovementVector=Vector2.Zero;}

    public void IssueAutonomousHarvestOrder(HarvestableResource target, Stockpile stockpile, Vector2 interactionPosition, Vector2 deliveryPosition)
    {
        AssignOrder(new HarvestResourceOrder(target, stockpile, interactionPosition, deliveryPosition), true);
    }

    public void IssueAutonomousBuildOrder(ConstructionSite target, Vector2 interactionPosition)
    {
        AssignOrder(new BuildOrder(target, interactionPosition), true);
    }

    public bool MoveTowardsGridPosition(Vector2 destination, double delta)
    {
        _destination = destination;
        Vector2 toDestination = destination - SimulationPosition;
        float distance = toDestination.Length();
        if (distance <= ArrivalThreshold)
        {
            MovementVector = Vector2.Zero;
            SimulationPosition = destination;
            UpdateRenderedPosition();
            return true;
        }

        float travelDistance = Mathf.Min(MovementSpeed * (float)delta, distance);
        MovementVector = toDestination / distance;
        SimulationPosition += MovementVector * travelDistance;
        UpdateRenderedPosition();
        bool arrived = travelDistance >= distance;
        if (arrived)
        {
            MovementVector = Vector2.Zero;
        }

        return arrived;
    }

    public int GetRemainingCarryCapacity(ResourceType resourceType)
    {
        if (CarriedAmount > 0 && CarriedResourceType != resourceType)
        {
            return 0;
        }

        int capacity = resourceType == ResourceType.Wood ? CarryCapacityWood : CarryCapacityFood;
        return Mathf.Max(0, capacity - CarriedAmount);
    }

    public void EatMeal()
    {
        Hunger = Mathf.Min(100, Hunger + MealRestoration);
    }

    public void IssueAutonomousEatOrder(SettlementInventory inventory, Stockpile stockpile, Vector2 interactionPosition)
    {
        AssignOrder(new EatOrder(inventory, stockpile, interactionPosition), true);
    }

    public bool TryAddCarriedResource(ResourceType resourceType, int amount)
    {
        if (amount <= 0 || amount > GetRemainingCarryCapacity(resourceType))
        {
            return false;
        }

        CarriedResourceType = resourceType;
        LastCarriedResourceType = resourceType;
        CarriedAmount += amount;
        RefreshVisual();
        return true;
    }

    public int RemoveCarriedResource()
    {
        int amount = CarriedAmount;
        LastCarriedResourceType = CarriedResourceType;
        CarriedAmount = 0;
        RefreshVisual();
        return amount;
    }

    public bool ContainsScreenPoint(Vector2 screenPoint)
    {
        Vector2 localPoint = GetGlobalTransformWithCanvas().AffineInverse() * screenPoint;
        return GetLocalSelectionBounds().HasPoint(localPoint);
    }

    public Rect2 GetScreenSelectionBounds()
    {
        Rect2 localBounds = GetLocalSelectionBounds();
        Transform2D toScreen = GetGlobalTransformWithCanvas();
        Vector2 topLeft = toScreen * localBounds.Position;
        Vector2 topRight = toScreen * new Vector2(localBounds.End.X, localBounds.Position.Y);
        Vector2 bottomRight = toScreen * localBounds.End;
        Vector2 bottomLeft = toScreen * new Vector2(localBounds.Position.X, localBounds.End.Y);

        float minX = Mathf.Min(Mathf.Min(topLeft.X, topRight.X), Mathf.Min(bottomRight.X, bottomLeft.X));
        float maxX = Mathf.Max(Mathf.Max(topLeft.X, topRight.X), Mathf.Max(bottomRight.X, bottomLeft.X));
        float minY = Mathf.Min(Mathf.Min(topLeft.Y, topRight.Y), Mathf.Min(bottomRight.Y, bottomLeft.Y));
        float maxY = Mathf.Max(Mathf.Max(topLeft.Y, topRight.Y), Mathf.Max(bottomRight.Y, bottomLeft.Y));
        return new Rect2(minX, minY, maxX - minX, maxY - minY);
    }

    private static Rect2 GetLocalSelectionBounds()
    {
        return new Rect2(-30, -118, 60, 122);
    }

    private void AssignOrder(ISurvivorOrder order, bool autonomous)
    {
        if(_dead)return;
        ReleaseAutonomousClaim();
        _currentOrder?.Cancel(this);
        _currentOrder = order;
        _isAutonomousOrder = autonomous;
        _currentOrder.Start(this);
        if (_currentOrder.IsComplete)
        {
            ReleaseAutonomousClaim();
            _currentOrder = null!;
        }
    }
    private void TryAutoDefend()
    {
        if(_currentOrder is not null&&!_isAutonomousOrder)return;
        Zombie nearest=GetTree().GetNodesInGroup(Zombie.GroupName).OfType<Zombie>().Where(z=>z.IsAlive&&z.SimulationPosition.DistanceSquaredTo(SimulationPosition)<=AutoDefenseRange*AutoDefenseRange).MinBy(z=>z.SimulationPosition.DistanceSquaredTo(SimulationPosition));
        if(nearest is not null&&(_currentOrder is null||CurrentOrderType!=SurvivorOrderType.AttackZombie))IssueAutonomousAttackOrder(nearest);
    }

    private void ReleaseAutonomousClaim()
    {
        if (_isAutonomousOrder && IsInsideTree())
        {
            (GetTree().GetFirstNodeInGroup(SettlementJobSystem.GroupName) as SettlementJobSystem)?.ReleaseClaim(this);
        }

        _isAutonomousOrder = false;
    }

    private void RefreshVisual()
    {
        if (IsInstanceValid(_visual))
        {
            _visual.QueueRedraw();
        }
    }

    private void UpdateRenderedPosition()
    {
        Vector2 projectedPosition = IsometricGrid.GridToScreen(SimulationPosition);
        if (!Position.IsEqualApprox(projectedPosition))
        {
            Position = projectedPosition;
        }
    }
}
