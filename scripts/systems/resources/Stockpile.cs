using AshwoodCounty.World;
using Godot;

namespace AshwoodCounty.Resources;

[Tool]
public partial class Stockpile : Node2D, IGridOccupant
{
    public const string GroupName = "settlement_stockpile";

    private Vector2 _gridPosition;
    private SettlementInventory _inventory = null!;

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

    [Export] public float InteractionRadius { get; set; } = 1.0f;
    [Export] public Vector2I Footprint { get; set; } = new(2, 2);
    public Vector2 WorldPosition => GridPosition + new Vector2(0.5f, 0.5f);
    public Vector2I OccupancyOrigin => new(Mathf.FloorToInt(GridPosition.X), Mathf.FloorToInt(GridPosition.Y));
    public Vector2I OccupancyFootprint => Footprint;

    public override void _Ready()
    {
        UpdateRenderedPosition();
        if (Engine.IsEditorHint())
        {
            return;
        }

        AddToGroup(GroupName);
        AddToGroup(GridOccupancy.OccupantGroup);
        _inventory = GetTree().GetFirstNodeInGroup(SettlementInventory.GroupName) as SettlementInventory;
    }

    public Vector2 GetInteractionPosition(int slot, int workerCount)
    {
        float angle = -Mathf.Pi * 0.5f + Mathf.Tau * slot / Mathf.Max(workerCount, 1);
        Vector2 offset = new(Mathf.Cos(angle), Mathf.Sin(angle));
        return WorldPosition + offset * InteractionRadius * 0.75f;
    }

    public void Deposit(ResourceType resourceType, int amount)
    {
        if (Engine.IsEditorHint() || amount <= 0)
        {
            return;
        }

        _inventory ??= GetTree().GetFirstNodeInGroup(SettlementInventory.GroupName) as SettlementInventory;
        if (_inventory is null)
        {
            GD.PushError("Stockpile could not find the settlement inventory.");
            return;
        }

        _inventory.Add(resourceType, amount);
        ResourceDepositFeedback feedback = new();
        AddChild(feedback);
        feedback.Initialize(resourceType, amount);
    }

    private void UpdateRenderedPosition()
    {
        Vector2 projectedPosition = IsometricGrid.GridToScreen(WorldPosition);
        if (!Position.IsEqualApprox(projectedPosition))
        {
            Position = projectedPosition;
        }
    }
}
