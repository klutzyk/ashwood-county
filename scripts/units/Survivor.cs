using AshwoodCounty.Resources;
using AshwoodCounty.Units.Orders;
using AshwoodCounty.World;
using Godot;

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

    private CanvasItem _selectionIndicator = null!;
    private CanvasItem _visual = null!;
    private Vector2 _destination;
    private ISurvivorOrder _currentOrder = null!;

    public bool IsSelected { get; private set; }
    public bool HasMoveOrder => CurrentOrderType == SurvivorOrderType.Move;
    public Vector2 Destination => _destination;
    public SurvivorOrderType CurrentOrderType => _currentOrder?.Type ?? SurvivorOrderType.None;
    public int CarriedAmount { get; private set; }
    public ResourceType CarriedResourceType { get; private set; } = ResourceType.Wood;
    public ResourceType LastCarriedResourceType { get; private set; } = ResourceType.Wood;

    public override void _Ready()
    {
        if (Engine.IsEditorHint())
        {
            SetPhysicsProcess(false);
            UpdateRenderedPosition();
            return;
        }

        AddToGroup(GroupName);
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

        if (_currentOrder is null)
        {
            return;
        }

        _currentOrder.Tick(this, delta);
        if (_currentOrder.IsComplete)
        {
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
        AssignOrder(new MoveOrder(destination));
    }

    public void IssueHarvestOrder(HarvestableResource target, Stockpile stockpile, Vector2 interactionPosition, Vector2 deliveryPosition)
    {
        AssignOrder(new HarvestResourceOrder(target, stockpile, interactionPosition, deliveryPosition));
    }

    public bool MoveTowardsGridPosition(Vector2 destination, double delta)
    {
        _destination = destination;
        Vector2 toDestination = destination - SimulationPosition;
        float distance = toDestination.Length();
        if (distance <= ArrivalThreshold)
        {
            SimulationPosition = destination;
            UpdateRenderedPosition();
            return true;
        }

        float travelDistance = Mathf.Min(MovementSpeed * (float)delta, distance);
        SimulationPosition += toDestination / distance * travelDistance;
        UpdateRenderedPosition();
        return travelDistance >= distance;
    }

    public int GetRemainingCarryCapacity(ResourceType resourceType)
    {
        if (CarriedAmount > 0 && CarriedResourceType != resourceType)
        {
            return 0;
        }

        return resourceType == ResourceType.Wood ? Mathf.Max(0, CarryCapacityWood - CarriedAmount) : 0;
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
        return new Rect2(-22, -72, 44, 76);
    }

    private void AssignOrder(ISurvivorOrder order)
    {
        _currentOrder?.Cancel(this);
        _currentOrder = order;
        _currentOrder.Start(this);
        if (_currentOrder.IsComplete)
        {
            _currentOrder = null!;
        }
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
