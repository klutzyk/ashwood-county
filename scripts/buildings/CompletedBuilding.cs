using AshwoodCounty.World;
using Godot;

namespace AshwoodCounty.Buildings;

[Tool]
public partial class CompletedBuilding : Node2D, IGridOccupant
{
    private Vector2I _gridOrigin;
    private Vector2I _footprint = new(3, 2);

    [Export]
    public Vector2I GridOrigin
    {
        get => _gridOrigin;
        set
        {
            _gridOrigin = value;
            UpdateRenderedPosition();
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
        }
    }

    [Export] public BuildingType BuildingType { get; set; } = BuildingType.Shelter;
    public Vector2I OccupancyOrigin => GridOrigin;
    public Vector2I OccupancyFootprint => Footprint;

    public override void _Ready()
    {
        UpdateRenderedPosition();
        if (!Engine.IsEditorHint())
        {
            AddToGroup(GridOccupancy.OccupantGroup);
        }
    }

    public void Initialize(BuildingDefinition definition, Vector2I origin)
    {
        BuildingType = definition.Type;
        GridOrigin = origin;
        Footprint = definition.Footprint;
    }

    private void UpdateRenderedPosition()
    {
        Vector2 projectedPosition = BuildingGridProjection.GetRenderAnchor(GridOrigin, Footprint);
        if (!Position.IsEqualApprox(projectedPosition))
        {
            Position = projectedPosition;
        }
    }
}
