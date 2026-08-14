#nullable enable

using System.Collections.Generic;
using AshwoodCounty.Resources;
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
    string TexturePath,float TargetHeight,LootTableDefinition LootTable,float SearchDuration=3.5f);
public sealed record BedDefinition(
    string Id,string DisplayName,string RoomId,Vector2 Position,Vector2 InteractionPosition,Rect2 Footprint,
    string TexturePath,float TargetHeight);

public sealed record LootOption(ResourceType? Resource,int Minimum,int Maximum,float Weight);
public readonly record struct LootStack(ResourceType Resource,int Amount);

public sealed class LootTableDefinition(string id,int rolls,params LootOption[] options)
{
    public string Id { get; }=id;
    public int Rolls { get; }=Mathf.Max(1,rolls);
    public IReadOnlyList<LootOption> Options { get; }=options;
    public IReadOnlyList<LootStack> Roll(ulong seed)
    {
        RandomNumberGenerator random=new(){Seed=seed};Dictionary<ResourceType,int> totals=[];float totalWeight=0;
        foreach(LootOption option in Options)totalWeight+=Mathf.Max(0,option.Weight);if(totalWeight<=0)return [];
        for(int roll=0;roll<Rolls;roll++)
        {
            float choice=random.RandfRange(0,totalWeight);LootOption selected=Options[^1];
            foreach(LootOption option in Options){choice-=Mathf.Max(0,option.Weight);if(choice<=0){selected=option;break;}}
            if(selected.Resource is not ResourceType resource)continue;int amount=random.RandiRange(Mathf.Max(0,selected.Minimum),Mathf.Max(selected.Minimum,selected.Maximum));if(amount>0)totals[resource]=totals.GetValueOrDefault(resource)+amount;
        }
        List<LootStack> result=[];foreach((ResourceType resource,int amount) in totals)result.Add(new LootStack(resource,amount));return result;
    }
}

public sealed record InteriorBuildingDefinition(
    string Id,string DisplayName,Vector2 ExteriorAnchor,Rect2 Footprint,string ExteriorTexturePath,float ExteriorTargetHeight,
    IReadOnlyList<RoomDefinition> Rooms,IReadOnlyList<WallDefinition> Walls,IReadOnlyList<DoorDefinition> Doors,
    IReadOnlyList<FurnitureDefinition> Furniture,IReadOnlyList<ContainerDefinition> Containers,IReadOnlyList<BedDefinition> Beds);

public sealed class DoorRuntimeState(InteriorDoorState state){public InteriorDoorState State { get; set; }=state;}
public sealed class ContainerRuntimeState
{
    public bool Searched { get; set; }
    public float SearchProgress { get; set; }
    public List<LootStack> RemainingLoot { get; }=[];
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
