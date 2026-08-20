#nullable enable

using System.Collections.Generic;
using AshwoodCounty.Items;
using Godot;

namespace AshwoodCounty.Buildings.Interiors;

public enum InteriorDoorState { Closed, Open, Locked, Barricaded, Broken }
public enum InteriorSearchState { Unknown, PartiallyExplored, Searched, Depleted }

public sealed record RoomDefinition(string Id,string DisplayName,Rect2 Bounds,string FloorTexturePath,Color FloorTint);
public sealed record WallDefinition(Vector2 Start,Vector2 End,string TexturePath,bool FlipVisual=false);
public sealed record DoorDefinition(
    string Id,string DisplayName,Vector2 Position,string FirstRoomId,string SecondRoomId,bool Exterior,
    string ClosedTexturePath,string OpenTexturePath,InteriorDoorState InitialState=InteriorDoorState.Closed,
    Vector2 OutsideApproachPoint=default,Vector2 InsideArrivalPoint=default,string WallId="");
public sealed record FurnitureDefinition(
    string Id,string DisplayName,Vector2 Position,Rect2 Footprint,string TexturePath,float TargetHeight,
    bool BlocksMovement=true,Color? Tint=null);
public sealed record ContainerDefinition(
    string Id,string DisplayName,string RoomId,Vector2 Position,Vector2 InteractionPosition,Rect2 Footprint,
    string TexturePath,float TargetHeight,ItemLootTableDefinition ItemLootTable,float SearchDuration=3.5f);
public sealed record BedDefinition(
    string Id,string DisplayName,string RoomId,Vector2 Position,Vector2 InteractionPosition,Rect2 Footprint,
    string TexturePath,float TargetHeight);

public sealed record InteriorBuildingDefinition(
    string Id,string DisplayName,Vector2 ExteriorAnchor,Rect2 Footprint,string ExteriorTexturePath,float ExteriorTargetHeight,float ExteriorTargetWidth,float ExteriorRotationDegrees,
    IReadOnlyList<RoomDefinition> Rooms,IReadOnlyList<WallDefinition> Walls,IReadOnlyList<DoorDefinition> Doors,
    IReadOnlyList<FurnitureDefinition> Furniture,IReadOnlyList<ContainerDefinition> Containers,IReadOnlyList<BedDefinition> Beds);

public sealed class DoorRuntimeState(InteriorDoorState state){public InteriorDoorState State { get; set; }=state;}
public sealed class ContainerRuntimeState
{
    public bool Searched { get; set; }
    public float SearchProgress { get; set; }
    public List<ItemStack> RemainingLoot { get; }=[];
}
public sealed class InteriorBuildingRuntimeState
{
    public HashSet<string> DiscoveredRooms { get; }=[];
    public Dictionary<string,DoorRuntimeState> Doors { get; }=[];
    public Dictionary<string,ContainerRuntimeState> Containers { get; }=[];
    public HashSet<string> UsedFurniture { get; }=[];
    public HashSet<string> RevealedThreatIds { get; }=[];
    public int ConcealedThreatCount { get; set; }
}
