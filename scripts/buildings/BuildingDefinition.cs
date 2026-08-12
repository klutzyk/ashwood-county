using AshwoodCounty.Resources;
using Godot;

namespace AshwoodCounty.Buildings;

public enum BuildingType
{
    Shelter
}

public sealed record BuildingDefinition(
    BuildingType Type,
    string DisplayName,
    Vector2I Footprint,
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
        new Vector2I(3, 2),
        ResourceType.Wood,
        30,
        6.0f,
        "res://scenes/buildings/ConstructionSite.tscn",
        "res://scenes/buildings/Shelter.tscn");
}
