using AshwoodCounty.Buildings;
using AshwoodCounty.Resources;
using AshwoodCounty.Threats;
using AshwoodCounty.Units;
using Godot;
using AshwoodCounty.World.Regions;

namespace AshwoodCounty.World.County;

/// <summary>Converts compact editor-local camp coordinates to county coordinates before gameplay Ready calls.</summary>
public partial class CountyCoordinateBootstrap : Node
{
    public static readonly Vector2 StartingAreaOffset=new(185,137);
    public override void _EnterTree()
    {
        Vector2 renderOffset=IsometricGrid.GridToScreen(StartingAreaOffset);
        GetNode<Node2D>("../World/Terrain").Position=renderOffset;
        Node objects=GetNode("../World/Objects");
        objects.GetNodeOrNull<Node2D>("RegionDressing")?.SetDeferred(Node2D.PropertyName.Position,renderOffset);
        foreach(Node child in objects.GetChildren())OffsetTree(child);
    }
    private static void OffsetTree(Node node)
    {
        switch(node)
        {
            case Survivor survivor: survivor.SimulationPosition+=StartingAreaOffset;break;
            case Zombie zombie: zombie.SimulationPosition+=StartingAreaOffset;break;
            case HarvestableResource resource: resource.GridPosition+=StartingAreaOffset;break;
            case ScavengeSource source: source.GridPosition+=StartingAreaOffset;break;
            case Stockpile stockpile: stockpile.GridPosition+=StartingAreaOffset;break;
            case CompletedBuilding building: building.BuildingPosition+=StartingAreaOffset;break;
            case Landmark landmark: landmark.GridPosition+=StartingAreaOffset;break;
        }
        foreach(Node child in node.GetChildren())OffsetTree(child);
    }
}
