using System.Collections.Generic;
using System.Linq;
using AshwoodCounty.World;
using Godot;

namespace AshwoodCounty.Resources;

[Tool]
public partial class HarvestableResource : Node2D, IGridOccupant
{
    public const string GroupName = "harvestable_resources";

    private readonly HashSet<ulong> _targetingWorkers = [];
    private readonly Dictionary<ulong, float> _workerProgress = [];
    private Vector2 _gridPosition;

    [Export]
    public Vector2 GridPosition
    {
        get => _gridPosition;
        set
        {
            _gridPosition = value;
            UpdateRenderedPosition();
        }
    }

    [Export] public ResourceType ResourceType { get; set; } = ResourceType.Wood;
    [Export] public int StartingAmount { get; set; } = 24;
    [Export] public float HarvestDuration { get; set; } = 1.5f;
    [Export] public float InteractionRadius { get; set; } = 0.9f;
    [Export] public Rect2 SelectionBounds { get; set; } = new(-72, -214, 144, 218);

    public int AvailableAmount { get; private set; }
    public bool IsDepleted => AvailableAmount <= 0;
    public bool IsHarvestable => !IsDepleted;
    public bool IsTargeted => _targetingWorkers.Count > 0;
    public bool IsDesignatedForHarvest { get; private set; }
    public bool IsDesignatedForChop => IsDesignatedForHarvest;
    public bool IsHovered { get; private set; }
    public bool IsWorkHighlighted { get; private set; }
    public float DisplayedHarvestProgress => _workerProgress.Count == 0 ? 0 : _workerProgress.Values.Max();
    public Vector2 WorldPosition => GridPosition + new Vector2(0.5f, 0.5f);
    public WorldFootprint OccupancyFootprint => new(WorldPosition - Vector2.One * 0.4f, Vector2.One * 0.8f);

    public override void _Ready()
    {
        UpdateRenderedPosition();
        if (Engine.IsEditorHint())
        {
            SetProcess(false);
            return;
        }

        AvailableAmount = Mathf.Max(0, StartingAmount);
        AddToGroup(GroupName);
        AddToGroup(GridOccupancy.OccupantGroup);
        RefreshVisual();
    }

    public int TryHarvest(ResourceType resourceType, int requestedAmount)
    {
        if (Engine.IsEditorHint() || resourceType != ResourceType || requestedAmount <= 0 || IsDepleted)
        {
            return 0;
        }

        int harvested = Mathf.Min(requestedAmount, AvailableAmount);
        AvailableAmount -= harvested;
        if (AvailableAmount <= 0)
        {
            IsDesignatedForHarvest = false;
        }
        RefreshVisual();
        return harvested;
    }

    public void BeginTargeting(ulong workerId)
    {
        if (Engine.IsEditorHint() || IsDepleted)
        {
            return;
        }

        _targetingWorkers.Add(workerId);
        RefreshVisual();
    }

    public void ReportHarvestProgress(ulong workerId, float progress)
    {
        if (Engine.IsEditorHint() || !_targetingWorkers.Contains(workerId))
        {
            return;
        }

        if (progress <= 0)
        {
            _workerProgress.Remove(workerId);
        }
        else
        {
            _workerProgress[workerId] = Mathf.Clamp(progress, 0, 1);
        }

        RefreshVisual();
    }

    public void EndTargeting(ulong workerId)
    {
        _targetingWorkers.Remove(workerId);
        _workerProgress.Remove(workerId);
        RefreshVisual();
    }

    public Vector2 GetInteractionPosition(int slot, int workerCount)
    {
        float angle = Mathf.Pi * 0.5f + Mathf.Tau * slot / Mathf.Max(workerCount, 1);
        Vector2 offset = new(Mathf.Cos(angle), Mathf.Sin(angle));
        return WorldPosition + offset * InteractionRadius * 0.8f;
    }

    public bool ContainsScreenPoint(Vector2 screenPoint)
    {
        Vector2 localPoint = GetGlobalTransformWithCanvas().AffineInverse() * screenPoint;
        return SelectionBounds.HasPoint(localPoint);
    }

    public void SetChopDesignated(bool designated)
    {
        SetHarvestDesignated(designated);
    }

    public void SetHarvestDesignated(bool designated)
    {
        IsDesignatedForHarvest = designated && IsHarvestable;
        RefreshVisual();
    }

    public void SetHovered(bool hovered)
    {
        if (IsHovered == hovered) return;
        IsHovered = hovered;
        RefreshVisual();
    }

    public void SetWorkHighlighted(bool highlighted)
    {
        if (IsWorkHighlighted == highlighted) return;
        IsWorkHighlighted = highlighted;
        RefreshVisual();
    }

    private void UpdateRenderedPosition()
    {
        Vector2 projectedPosition = IsometricGrid.GridToScreen(WorldPosition);
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
