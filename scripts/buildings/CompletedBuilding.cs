using AshwoodCounty.World;
using Godot;

namespace AshwoodCounty.Buildings;

[Tool]
public partial class CompletedBuilding : Node2D, IGridOccupant
{
    private Vector2 _buildingPosition;
    private Vector2 _footprintSize = new(3, 2);

    [Export]
    public Vector2 BuildingPosition
    {
        get => _buildingPosition;
        set
        {
            _buildingPosition = value;
            UpdateRenderedPosition();
            QueueRedraw();
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
            QueueRedraw();
        }
    }

    [Export] public BuildingType BuildingType { get; set; } = BuildingType.Shelter;
    public WorldFootprint OccupancyFootprint => new(BuildingPosition, FootprintSize);

    public override void _Ready()
    {
        UpdateRenderedPosition();
        if (!Engine.IsEditorHint())
        {
            AddToGroup(GridOccupancy.OccupantGroup);
        }
    }

    public void Initialize(BuildingDefinition definition, Vector2 position)
    {
        BuildingType = definition.Type;
        BuildingPosition = position;
        FootprintSize = definition.FootprintSize;
    }

    public override void _Draw()
    {
        if (!Engine.IsEditorHint())
        {
            return;
        }

        Vector2 anchor = BuildingGridProjection.GetRenderAnchor(BuildingPosition, FootprintSize);
        Vector2[] footprint = IsometricGrid.ProjectRectangle(BuildingPosition, FootprintSize);
        for (int index = 0; index < footprint.Length; index++)
        {
            footprint[index] -= anchor;
        }

        DrawColoredPolygon(footprint, new Color(0.35f, 0.8f, 0.48f, 0.18f));
        DrawPolyline([footprint[0], footprint[1], footprint[2], footprint[3], footprint[0]], new Color("#69d77d"), 2, true);
    }

    private void UpdateRenderedPosition()
    {
        Vector2 projectedPosition = BuildingGridProjection.GetRenderAnchor(BuildingPosition, FootprintSize);
        if (!Position.IsEqualApprox(projectedPosition))
        {
            Position = projectedPosition;
        }
    }
}
