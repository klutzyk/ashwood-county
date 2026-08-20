using AshwoodCounty.World;
using Godot;

namespace AshwoodCounty.Resources;

/// <summary>A finite, designatable cache searched by one survivor at a time.</summary>
[Tool]
public partial class ScavengeSource : Node2D, IGridOccupant
{
    public const string GroupName = "scavenge_sources";

    private Vector2 _gridPosition;
    private ulong _claimingWorker;
    private float _searchProgress;

    [Export]
    public Vector2 GridPosition
    {
        get => _gridPosition;
        set { _gridPosition = value; UpdateRenderedPosition(); }
    }

    [Export] public ResourceType LootType { get; set; } = ResourceType.Materials;
    [Export] public string DisplayName { get; set; } = "";
    [Export] public int StartingAmount { get; set; } = 12;
    [Export] public float SearchDuration { get; set; } = 4.0f;
    [Export] public float InteractionRadius { get; set; } = 0.75f;
    [Export] public bool DesignatedAtStart { get; set; }
    [Export] public Rect2 SelectionBounds { get; set; } = new(-48, -70, 96, 76);

    public int AvailableAmount { get; private set; }
    public bool IsDepleted => AvailableAmount <= 0;
    public bool IsDesignatedForScavenging { get; private set; }
    public bool IsClaimed => _claimingWorker != 0;
    public bool IsHovered { get; private set; }
    public bool IsWorkHighlighted { get; private set; }
    public float DisplayedSearchProgress => _searchProgress;
    public Vector2 WorldPosition => GridPosition + new Vector2(0.5f, 0.5f);
    public WorldFootprint OccupancyFootprint => new(WorldPosition - Vector2.One * 0.35f, Vector2.One * 0.7f);
    public string ResolvedDisplayName => string.IsNullOrWhiteSpace(DisplayName) ? Name : DisplayName;

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

    public override void _Ready()
    {
        UpdateRenderedPosition();
        if (Engine.IsEditorHint()) { SetProcess(false); return; }
        AvailableAmount = Mathf.Max(0, StartingAmount);
        IsDesignatedForScavenging = DesignatedAtStart && !IsDepleted;
        AddToGroup(GroupName);
        AddToGroup(GridOccupancy.OccupantGroup);
        RefreshVisual();
    }

    public void SetScavengeDesignated(bool designated)
    {
        if (Engine.IsEditorHint()) return;
        IsDesignatedForScavenging = designated && !IsDepleted;
        RefreshVisual();
    }

    public bool TryClaim(ulong workerId)
    {
        if (Engine.IsEditorHint() || IsDepleted || !IsDesignatedForScavenging || (_claimingWorker != 0 && _claimingWorker != workerId)) return false;
        _claimingWorker = workerId;
        RefreshVisual();
        return true;
    }

    public void ReportSearchProgress(ulong workerId, float progress)
    {
        if (_claimingWorker != workerId) return;
        _searchProgress = Mathf.Clamp(progress, 0, 1);
        RefreshVisual();
    }

    public int TakeLoot(ulong workerId, int requestedAmount)
    {
        if (_claimingWorker != workerId || requestedAmount <= 0 || IsDepleted) return 0;
        int amount = Mathf.Min(requestedAmount, AvailableAmount);
        AvailableAmount -= amount;
        _searchProgress = 0;
        if (IsDepleted) IsDesignatedForScavenging = false;
        RefreshVisual();
        return amount;
    }

    public void ReleaseClaim(ulong workerId)
    {
        if (_claimingWorker != workerId) return;
        _claimingWorker = 0;
        _searchProgress = 0;
        RefreshVisual();
    }

    public Vector2 GetInteractionPosition() => WorldPosition + new Vector2(0, InteractionRadius);

    public bool ContainsScreenPoint(Vector2 screenPoint)
    {
        Vector2 localPoint = GetGlobalTransformWithCanvas().AffineInverse() * screenPoint;
        return SelectionBounds.HasPoint(localPoint);
    }

    private void UpdateRenderedPosition()
    {
        Vector2 projected = IsometricGrid.GridToScreen(WorldPosition);
        if (!Position.IsEqualApprox(projected)) Position = projected;
    }

    private void RefreshVisual() => GetNodeOrNull<CanvasItem>("Visual")?.QueueRedraw();
}
