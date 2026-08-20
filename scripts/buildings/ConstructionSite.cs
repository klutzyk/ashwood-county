using System.Collections.Generic;
using AshwoodCounty.Resources;
using AshwoodCounty.World;
using Godot;

namespace AshwoodCounty.Buildings;

[Tool]
public partial class ConstructionSite : Node2D, IGridOccupant
{
    public const string GroupName = "construction_sites";

    private readonly HashSet<ulong> _activeBuilders = [];
    private Vector2 _buildingPosition;
    private Vector2 _footprintSize = new(3, 2);
    private BuildingDefinition _definition = BuildingCatalog.Shelter;
    private GridOccupancy _occupancy = null!;
    private SettlementInventory _inventory = null!;
    private bool _resourcesPaid;

    [Export]
    public Vector2 BuildingPosition
    {
        get => _buildingPosition;
        set
        {
            _buildingPosition = value;
            UpdateRenderedPosition();
            RefreshVisual();
        }
    }

    [Export]
    public Vector2 FootprintSize
    {
        get => _footprintSize;
        set
        {
            _footprintSize = value;
            UpdateRenderedPosition();
            RefreshVisual();
        }
    }

    [Export] public float RequiredWork { get; set; } = 6.0f;

    public WorldFootprint OccupancyFootprint => new(BuildingPosition, FootprintSize);
    public float CurrentWork { get; private set; }
    public float Progress => RequiredWork <= 0 ? 1 : Mathf.Clamp(CurrentWork / RequiredWork, 0, 1);
    public bool IsCompleted { get; private set; }
    public bool IsCancelled { get; private set; }
    public bool IsAvailableForBuilding => !IsCompleted && !IsCancelled;

    public override void _Ready()
    {
        UpdateRenderedPosition();
        if (Engine.IsEditorHint())
        {
            SetProcess(false);
            return;
        }

        AddToGroup(GroupName);
        if (GetTree().GetFirstNodeInGroup(WorldNavigationService.GroupName) is WorldNavigationService navigationService)
        {
            navigationService.RegisterObstacle(OccupancyFootprint, this, allowTraversalInside: true);
        }
    }

    public override void _ExitTree()
    {
        if (IsInsideTree() && GetTree().GetFirstNodeInGroup(WorldNavigationService.GroupName) is WorldNavigationService navigationService)
        {
            navigationService.UnregisterObstacle(this);
        }
    }

    public void Initialize(BuildingDefinition definition, Vector2 position, GridOccupancy occupancy, SettlementInventory inventory)
    {
        _definition = definition;
        BuildingPosition = position;
        FootprintSize = definition.FootprintSize;
        RequiredWork = definition.RequiredConstructionWork;
        _occupancy = occupancy;
        _inventory = inventory;
        _resourcesPaid = true;
        CurrentWork = 0;
        IsCompleted = false;
        IsCancelled = false;
        RefreshVisual();
    }

    public void BeginBuilding(ulong workerId)
    {
        if (!Engine.IsEditorHint() && IsAvailableForBuilding)
        {
            _activeBuilders.Add(workerId);
            RefreshVisual();
        }
    }

    public void EndBuilding(ulong workerId)
    {
        _activeBuilders.Remove(workerId);
        RefreshVisual();
    }

    public void AddConstructionWork(ulong workerId, float amount)
    {
        if (!IsAvailableForBuilding || !_activeBuilders.Contains(workerId) || amount <= 0)
        {
            return;
        }

        CurrentWork = Mathf.Min(RequiredWork, CurrentWork + amount);
        RefreshVisual();
        if (CurrentWork >= RequiredWork)
        {
            CompleteConstruction();
        }
    }

    public bool CancelConstruction()
    {
        if (Engine.IsEditorHint() || !IsAvailableForBuilding)
        {
            return false;
        }

        IsCancelled = true;
        _activeBuilders.Clear();
        _occupancy?.Release(this);
        if (_resourcesPaid)
        {
            _inventory?.Add(_definition.CostResource, _definition.ResourceCost);
            _resourcesPaid = false;
        }

        QueueFree();
        return true;
    }

    public Vector2 GetInteractionPosition(int slot, int workerCount)
    {
        List<Vector2> positions = CreatePerimeterPositions();
        return positions[slot % positions.Count];
    }

    public bool ContainsScreenPoint(Vector2 screenPoint)
    {
        Vector2 localPoint = GetGlobalTransformWithCanvas().AffineInverse() * screenPoint;
        float halfWidth = (FootprintSize.X + FootprintSize.Y) * IsometricGrid.TileWidth * 0.28f;
        float height = (FootprintSize.X + FootprintSize.Y) * IsometricGrid.TileHeight * 0.5f + 75;
        return new Rect2(-halfWidth, -height, halfWidth * 2, height + 10).HasPoint(localPoint);
    }

    private void CompleteConstruction()
    {
        if (IsCompleted || IsCancelled)
        {
            return;
        }

        IsCompleted = true;
        CurrentWork = RequiredWork;
        _activeBuilders.Clear();

        PackedScene scene = GD.Load<PackedScene>(_definition.CompletedBuildingScenePath);
        CompletedBuilding completedBuilding = scene.Instantiate<CompletedBuilding>();
        completedBuilding.Initialize(_definition, BuildingPosition);
        GetParent().AddChild(completedBuilding);
        if (!_occupancy.Transfer(this, completedBuilding))
        {
            GD.PushError("Construction completed but grid occupancy could not be transferred.");
        }

        QueueFree();
    }

    private List<Vector2> CreatePerimeterPositions()
    {
        List<Vector2> positions = [];
        int horizontalSlots = Mathf.Max(1, Mathf.CeilToInt(FootprintSize.X));
        int verticalSlots = Mathf.Max(1, Mathf.CeilToInt(FootprintSize.Y));
        for (int x = 0; x < horizontalSlots; x++)
        {
            float offset = FootprintSize.X * (x + 0.5f) / horizontalSlots;
            AddIfInside(positions, new Vector2(BuildingPosition.X + offset, BuildingPosition.Y - 0.35f));
            AddIfInside(positions, new Vector2(BuildingPosition.X + offset, BuildingPosition.Y + FootprintSize.Y + 0.35f));
        }

        for (int y = 0; y < verticalSlots; y++)
        {
            float offset = FootprintSize.Y * (y + 0.5f) / verticalSlots;
            AddIfInside(positions, new Vector2(BuildingPosition.X - 0.35f, BuildingPosition.Y + offset));
            AddIfInside(positions, new Vector2(BuildingPosition.X + FootprintSize.X + 0.35f, BuildingPosition.Y + offset));
        }

        if (positions.Count == 0)
        {
            positions.Add(BuildingGridProjection.GetFootprintCenter(BuildingPosition, FootprintSize));
        }

        return positions;
    }

    private static void AddIfInside(List<Vector2> positions, Vector2 position)
    {
        if (IsometricWorld.IsGridPositionInBounds(position))
        {
            positions.Add(position);
        }
    }

    private void UpdateRenderedPosition()
    {
        Vector2 projectedPosition = BuildingGridProjection.GetRenderAnchor(BuildingPosition, FootprintSize);
        if (!Position.IsEqualApprox(projectedPosition))
        {
            Position = projectedPosition;
        }
    }

    private void RefreshVisual()
    {
        GetNodeOrNull<CanvasItem>("Visual")?.QueueRedraw();
    }
}
