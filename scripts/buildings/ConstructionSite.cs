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
    private Vector2I _gridOrigin;
    private Vector2I _footprint = new(3, 2);
    private BuildingDefinition _definition = BuildingCatalog.Shelter;
    private GridOccupancy _occupancy = null!;
    private SettlementInventory _inventory = null!;
    private bool _resourcesPaid;

    [Export]
    public Vector2I GridOrigin
    {
        get => _gridOrigin;
        set
        {
            _gridOrigin = value;
            UpdateRenderedPosition();
            RefreshVisual();
        }
    }

    [Export]
    public Vector2I Footprint
    {
        get => _footprint;
        set
        {
            _footprint = value;
            UpdateRenderedPosition();
            RefreshVisual();
        }
    }

    [Export] public float RequiredWork { get; set; } = 6.0f;

    public Vector2I OccupancyOrigin => GridOrigin;
    public Vector2I OccupancyFootprint => Footprint;
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
    }

    public void Initialize(BuildingDefinition definition, Vector2I origin, GridOccupancy occupancy, SettlementInventory inventory)
    {
        _definition = definition;
        GridOrigin = origin;
        Footprint = definition.Footprint;
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
        float halfWidth = (Footprint.X + Footprint.Y) * IsometricGrid.TileWidth * 0.28f;
        float height = (Footprint.X + Footprint.Y) * IsometricGrid.TileHeight * 0.5f + 75;
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
        completedBuilding.Initialize(_definition, GridOrigin);
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
        for (int x = 0; x < Footprint.X; x++)
        {
            AddIfInside(positions, new Vector2(GridOrigin.X + x + 0.5f, GridOrigin.Y - 0.35f));
            AddIfInside(positions, new Vector2(GridOrigin.X + x + 0.5f, GridOrigin.Y + Footprint.Y + 0.35f));
        }

        for (int y = 0; y < Footprint.Y; y++)
        {
            AddIfInside(positions, new Vector2(GridOrigin.X - 0.35f, GridOrigin.Y + y + 0.5f));
            AddIfInside(positions, new Vector2(GridOrigin.X + Footprint.X + 0.35f, GridOrigin.Y + y + 0.5f));
        }

        if (positions.Count == 0)
        {
            positions.Add(BuildingGridProjection.GetFootprintCenter(GridOrigin, Footprint));
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
        Vector2 projectedPosition = BuildingGridProjection.GetRenderAnchor(GridOrigin, Footprint);
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
