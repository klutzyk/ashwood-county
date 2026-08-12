using Godot;
namespace AshwoodCounty.World;
public enum RegionAvailability { Current, Planned }
public sealed record RegionDefinition(string Id,string Name,string Description,Vector2 CountyPosition,RegionAvailability Availability);
public static class RegionCatalog
{
    public static readonly RegionDefinition[] All=[
        new("outskirts","Ashwood Outskirts","Rural foothold west of Ashwood; current settlement region.",new(.48f,.48f),RegionAvailability.Current),
        new("farm_edge","Farm District Edge","Open fields, fence lines, and scattered farm woodland.",new(.27f,.48f),RegionAvailability.Planned),
        new("mill_creek","Mill Creek Woodland","Dense timber and logging traces near the Old Mill approaches.",new(.30f,.68f),RegionAvailability.Planned)
    ];
}
