using AshwoodCounty.Resources;
using Godot;

namespace AshwoodCounty.Buildings;

public enum BuildingType
{
    Shelter,
    ProvisionsShed
}

public sealed record BuildingDefinition(
    BuildingType Type,
    string DisplayName,
    Vector2 FootprintSize,
    ResourceType CostResource,
    int ResourceCost,
    float RequiredConstructionWork,
    string ConstructionSiteScenePath,
    string CompletedBuildingScenePath);

public static class BuildingCatalog
{
    public static readonly BuildingDefinition Shelter = new(
        BuildingType.Shelter,
        "Shelter",
        new Vector2(3, 2),
        ResourceType.Wood,
        30,
        6.0f,
        "res://scenes/buildings/ConstructionSite.tscn",
        "res://scenes/buildings/Shelter.tscn");
    public static readonly BuildingDefinition ProvisionsShed = new(
        BuildingType.ProvisionsShed, "Provisions Shed", new Vector2(2, 2), ResourceType.Wood, 20, 4.5f,
        "res://scenes/buildings/ConstructionSite.tscn", "res://scenes/buildings/ProvisionsShed.tscn");
}
