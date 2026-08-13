using AshwoodCounty.World;
using Godot;
using AshwoodCounty.World.Regions;
using AshwoodCounty.World.County;
using System.Collections.Generic;

namespace AshwoodCounty.Buildings;

[Tool]
public partial class CompletedBuilding : Node2D, IGridOccupant
{
    public const string GroupName = "completed_buildings";

    private Vector2 _buildingPosition;
    private Vector2 _footprintSize = new(3, 2);
    private readonly Dictionary<ulong, int> _restingSurvivors = [];

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
    [Export] public string RegionId { get; set; } = "outskirts";
    [Export(PropertyHint.Range, "0,20,1")] public int RestCapacity { get; set; } = 4;
    public WorldFootprint OccupancyFootprint => new(BuildingPosition, FootprintSize);
    public bool ProvidesRest => BuildingType == BuildingType.Shelter && RestCapacity > 0;
    public int AvailableRestSlots => Mathf.Max(0, RestCapacity - _restingSurvivors.Count);

    public override void _Ready()
    {
        UpdateRenderedPosition();
        if (!Engine.IsEditorHint())
        {
            CountyWorld county=GetTree().Root.FindChild("CountyWorld",true,false) as CountyWorld;
            if(county is not null)RegionId=county.GetRegionAt(BuildingPosition).Id;
            AddToGroup(GridOccupancy.OccupantGroup);
            AddToGroup(GroupName);
        }
    }

    public bool TryReserveRestSlot(ulong survivorId, out Vector2 restPosition)
    {
        restPosition = BuildingPosition + FootprintSize * 0.5f;
        if (!ProvidesRest || (!_restingSurvivors.ContainsKey(survivorId) && _restingSurvivors.Count >= RestCapacity))
        {
            return false;
        }

        if (!_restingSurvivors.TryGetValue(survivorId, out int slot))
        {
            slot = 0;
            while (_restingSurvivors.ContainsValue(slot)) slot++;
            _restingSurvivors[survivorId] = slot;
        }
        int columns = Mathf.Max(1, Mathf.CeilToInt(Mathf.Sqrt(RestCapacity)));
        int rows = Mathf.Max(1, Mathf.CeilToInt((float)RestCapacity / columns));
        restPosition = BuildingPosition + new Vector2(
            (slot % columns + 0.5f) * FootprintSize.X / columns,
            (slot / columns + 0.5f) * FootprintSize.Y / rows);
        return true;
    }

    public void ReleaseRestSlot(ulong survivorId) => _restingSurvivors.Remove(survivorId);

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
