#nullable enable

using System;
using System.Collections.Generic;
using AshwoodCounty.Resources;
using Godot;

namespace AshwoodCounty.Buildings.Interiors;

public enum InteriorDoorState { Closed, Open, Locked, Barricaded, Broken }
public enum InteriorSearchState { Unknown, PartiallyExplored, Searched, Depleted }
public enum InteriorFurnitureUse { Decoration, Bed }

public sealed record RoomDefinition(
    string Id,
    string DisplayName,
    Rect2 Bounds,
    string FloorTexturePath,
    Color FloorTint);

public sealed record WallDefinition(
    Vector2 Start,
    Vector2 End,
    string TexturePath,
    bool FlipVisual = false);

public sealed record DoorDefinition(
    string Id,
    string DisplayName,
    Vector2 Position,
    string FirstRoomId,
    string SecondRoomId,
    bool Exterior,
    string ClosedTexturePath,
    string OpenTexturePath,
    InteriorDoorState InitialState = InteriorDoorState.Closed);

public sealed record FurnitureDefinition(
    string Id,
    string DisplayName,
    Vector2 Position,
    Rect2 Footprint,
    string TexturePath,
    float TargetHeight,
    bool BlocksMovement = true,
    Color? Tint = null);

public sealed record ContainerDefinition(
    string Id,
    string DisplayName,
    string RoomId,
    Vector2 Position,
    Vector2 InteractionPosition,
    Rect2 Footprint,
    string TexturePath,
    float TargetHeight,
    LootTableDefinition LootTable,
    float SearchDuration = 3.5f);

public sealed record BedDefinition(
    string Id,
    string DisplayName,
    string RoomId,
    Vector2 Position,
    Vector2 InteractionPosition,
    Rect2 Footprint,
    string TexturePath,
    float TargetHeight);

public sealed record LootOption(ResourceType? Resource, int Minimum, int Maximum, float Weight);

public sealed class LootTableDefinition(string id, int rolls, params LootOption[] options)
{
    public string Id { get; } = id;
    public int Rolls { get; } = Mathf.Max(1, rolls);
    public IReadOnlyList<LootOption> Options { get; } = options;

    public IReadOnlyList<LootStack> Roll(ulong seed)
    {
        RandomNumberGenerator random = new() { Seed = seed };
        Dictionary<ResourceType, int> totals = [];
        float totalWeight = 0;
        foreach (LootOption option in Options) totalWeight += Mathf.Max(0, option.Weight);
        if (totalWeight <= 0) return [];

        for (int roll = 0; roll < Rolls; roll++)
        {
            float choice = random.RandfRange(0, totalWeight);
            LootOption selected = Options[^1];
            foreach (LootOption option in Options)
            {
                choice -= Mathf.Max(0, option.Weight);
                if (choice <= 0) { selected = option; break; }
            }

            if (selected.Resource is not ResourceType resource) continue;
            int amount = random.RandiRange(Mathf.Max(0, selected.Minimum), Mathf.Max(selected.Minimum, selected.Maximum));
            if (amount > 0) totals[resource] = totals.GetValueOrDefault(resource) + amount;
        }

        List<LootStack> result = [];
        foreach ((ResourceType resource, int amount) in totals) result.Add(new LootStack(resource, amount));
        return result;
    }
}

public readonly record struct LootStack(ResourceType Resource, int Amount);

public sealed record InteriorBuildingDefinition(
    string Id,
    string DisplayName,
    Vector2 ExteriorAnchor,
    Rect2 Footprint,
    string ExteriorTexturePath,
    float ExteriorTargetHeight,
    IReadOnlyList<RoomDefinition> Rooms,
    IReadOnlyList<WallDefinition> Walls,
    IReadOnlyList<DoorDefinition> Doors,
    IReadOnlyList<FurnitureDefinition> Furniture,
    IReadOnlyList<ContainerDefinition> Containers,
    IReadOnlyList<BedDefinition> Beds);

public sealed class DoorRuntimeState(InteriorDoorState state)
{
    public InteriorDoorState State { get; set; } = state;
}

public sealed class ContainerRuntimeState
{
    public bool Searched { get; set; }
    public float SearchProgress { get; set; }
    public List<LootStack> RemainingLoot { get; } = [];
}

public sealed class InteriorBuildingRuntimeState
{
    public HashSet<string> DiscoveredRooms { get; } = [];
    public Dictionary<string, DoorRuntimeState> Doors { get; } = [];
    public Dictionary<string, ContainerRuntimeState> Containers { get; } = [];
    public HashSet<string> UsedFurniture { get; } = [];
    public HashSet<string> RevealedThreatIds { get; } = [];
    public int ConcealedThreatCount { get; set; }
}

public static class ResidentialInteriorCatalog
{
    private const string Root = "res://assets/art/interiors/residential/";

    private static readonly LootTableDefinition RefrigeratorLoot = new("residential_fridge", 2,
        new(ResourceType.Food, 1, 3, .63f), new(ResourceType.Medicine, 1, 1, .08f), new(null, 0, 0, .29f));
    private static readonly LootTableDefinition CupboardLoot = new("residential_cupboard", 2,
        new(ResourceType.Food, 1, 2, .48f), new(ResourceType.Materials, 1, 2, .25f), new(null, 0, 0, .27f));
    private static readonly LootTableDefinition BathroomLoot = new("residential_bathroom", 2,
        new(ResourceType.Medicine, 1, 2, .34f), new(ResourceType.Materials, 1, 1, .18f), new(null, 0, 0, .48f));
    private static readonly LootTableDefinition BedroomLoot = new("residential_bedroom", 2,
        new(ResourceType.Materials, 1, 2, .37f), new(ResourceType.Medicine, 1, 1, .10f), new(ResourceType.Food, 1, 1, .08f), new(null, 0, 0, .45f));
    private static readonly LootTableDefinition UtilityLoot = new("residential_utility", 3,
        new(ResourceType.Materials, 1, 3, .62f), new(ResourceType.Medicine, 1, 1, .06f), new(null, 0, 0, .32f));

    public static readonly InteriorBuildingDefinition ReferenceHouse = new(
        "ashwood_house_220_155",
        "Abandoned Family Home",
        new Vector2(220, 155),
        new Rect2(216.5f, 151.5f, 7f, 6f),
        "res://assets/art/buildings/residential/abandoned_house_08.png",
        420f,
        [
            new("kitchen", "Kitchen / Dining", new Rect2(216.7f, 151.7f, 2.9f, 2.6f), Root + "surfaces/floor_tile_cream_01.png", new Color("d7c7a8")),
            new("living", "Living Room", new Rect2(216.7f, 154.5f, 2.9f, 2.8f), Root + "surfaces/floor_wood_dark_01.png", new Color("b59169")),
            new("hall", "Hallway", new Rect2(219.9f, 151.7f, .55f, 5.6f), Root + "surfaces/floor_wood_light_01.png", new Color("bca478")),
            new("bedroom_one", "Front Bedroom", new Rect2(220.75f, 151.7f, 2.55f, 2.2f), Root + "surfaces/floor_wood_light_01.png", new Color("af9974")),
            new("bathroom", "Bathroom", new Rect2(220.75f, 154.1f, 2.55f, 1.15f), Root + "surfaces/floor_checker_01.png", new Color("b9b6aa")),
            new("bedroom_two", "Rear Bedroom", new Rect2(220.75f, 155.55f, 2.55f, 1.75f), Root + "surfaces/floor_wood_light_01.png", new Color("a9916e")),
        ],
        [
            W(216.5f,151.5f,223.5f,151.5f), W(216.5f,151.5f,216.5f,157.5f,true), W(223.5f,151.5f,223.5f,157.5f,true),
            W(216.5f,157.5f,218.0f,157.5f), W(218.85f,157.5f,223.5f,157.5f),
            W(219.75f,151.5f,219.75f,152.6f,true), W(219.75f,153.4f,219.75f,155.15f,true), W(219.75f,156.0f,219.75f,157.5f,true),
            W(220.6f,151.5f,220.6f,152.35f,true), W(220.6f,153.2f,220.6f,154.45f,true), W(220.6f,155.25f,220.6f,156.15f,true), W(220.6f,157.0f,220.6f,157.5f,true),
            W(216.5f,154.4f,217.6f,154.4f), W(218.55f,154.4f,219.75f,154.4f),
            W(220.6f,154.0f,223.5f,154.0f), W(220.6f,155.4f,223.5f,155.4f),
        ],
        [
            D("front_door", "Front Door", 218.42f,157.5f, "outside", "living", true),
            D("kitchen_door", "Kitchen Door", 219.75f,153.0f, "kitchen", "hall"),
            D("living_door", "Living Room Door", 219.75f,155.58f, "living", "hall", false, InteriorDoorState.Open),
            D("bedroom_one_door", "Front Bedroom Door", 220.6f,152.78f, "hall", "bedroom_one"),
            D("bathroom_door", "Bathroom Door", 220.6f,154.85f, "hall", "bathroom"),
            D("bedroom_two_door", "Rear Bedroom Door", 220.6f,156.58f, "hall", "bedroom_two"),
        ],
        [
            F("living_sofa", "Worn Sofa", 217.25f,154.95f,1.15f,.62f,"living/sofa_plaid_01.png",88),
            F("living_armchair", "Armchair", 218.72f,154.92f,.62f,.62f,"living/armchair_green_01.png",78),
            F("living_coffee", "Coffee Table", 218.05f,155.78f,.82f,.50f,"living/coffee_table_01.png",52),
            F("living_tv", "Television", 217.15f,156.72f,.72f,.42f,"living/television_01.png",66),
            F("living_bookcase", "Bookcase", 219.25f,156.78f,.38f,.48f,"living/bookcase_tall_01.png",96),
            F("living_rug", "Faded Rug", 218.05f,156.15f,.05f,.05f,"surfaces/rug_red_01.png",54,false,new Color(1,1,1,.72f)),
            F("kitchen_stove", "Stove", 217.72f,152.0f,.58f,.62f,"kitchen/stove_01.png",78),
            F("kitchen_sink", "Sink Counter", 218.65f,151.95f,.78f,.58f,"kitchen/counter_sink_01.png",76),
            F("kitchen_table", "Dining Table", 218.25f,153.45f,1.05f,.78f,"living/dining_table_01.png",82),
            F("bath_tub", "Bathtub", 221.35f,154.28f,1.15f,.58f,"bathroom/bathtub_01.png",74),
            F("bath_toilet", "Toilet", 222.88f,154.82f,.42f,.42f,"bathroom/toilet_01.png",61),
            F("rear_dresser", "Bedside Table", 222.88f,156.92f,.42f,.42f,"bedroom/dresser_01.png",55),
            F("hall_coatstand", "Coat Stand", 220.18f,156.95f,.25f,.25f,"clutter/coat_stand_01.png",78,false),
            F("abandoned_boxes", "Abandoned Boxes", 216.95f,153.82f,.48f,.42f,"clutter/storage_boxes_01.png",52,false),
            F("disturbed_rug", "Disturbed Possessions", 222.05f,156.5f,.05f,.05f,"clutter/bloodied_rug_01.png",42,false,new Color(1,1,1,.72f)),
        ],
        [
            C("fridge", "Refrigerator", "kitchen",217.0f,152.12f,217.05f,152.95f,.58f,.62f,"kitchen/refrigerator_white_01.png",91,RefrigeratorLoot),
            C("cupboard", "Kitchen Cupboards", "kitchen",219.15f,152.05f,219.1f,152.82f,.65f,.55f,"living/bookcase_medium_01.png",72,CupboardLoot),
            C("bathroom_cabinet", "Bathroom Cabinet", "bathroom",222.85f,154.3f,222.45f,154.8f,.42f,.40f,"bathroom/bathroom_sink_01.png",62,BathroomLoot),
            C("front_dresser", "Bedroom Dresser", "bedroom_one",222.9f,153.52f,222.45f,153.45f,.48f,.42f,"bedroom/dresser_01.png",57,BedroomLoot),
            C("utility_shelf", "Utility Shelving", "kitchen",216.95f,153.65f,217.5f,153.72f,.48f,.55f,"utility/supply_shelf_01.png",93,UtilityLoot,4.2f),
        ],
        [
            B("bed_one", "Front Bedroom Bed", "bedroom_one",221.45f,152.25f,222.4f,152.9f,1.35f,.82f,"bedroom/bed_blue_01.png",94),
            B("bed_two", "Rear Bedroom Bed", "bedroom_two",221.45f,155.9f,222.45f,156.88f,1.32f,.78f,"bedroom/bed_single_blue_01.png",82),
        ]);

    private static WallDefinition W(float x1,float y1,float x2,float y2,bool flip=false) =>
        new(new Vector2(x1,y1),new Vector2(x2,y2),Root+(flip?"structure/wall_plain_blue_01.png":"structure/wall_plain_cream_01.png"),flip);
    private static DoorDefinition D(string id,string name,float x,float y,string firstRoom,string secondRoom,bool exterior=false,InteriorDoorState state=InteriorDoorState.Closed) =>
        new(id,name,new Vector2(x,y),firstRoom,secondRoom,exterior,Root+"structure/door_closed_brown_01.png",Root+"structure/door_frame_open_01.png",state);
    private static FurnitureDefinition F(string id,string name,float x,float y,float w,float h,string path,float height,bool block=true,Color? tint=null) =>
        new(id,name,new Vector2(x,y),new Rect2(x-w*.5f,y-h*.5f,w,h),Root+path,height,block,tint);
    private static ContainerDefinition C(string id,string name,string room,float x,float y,float ix,float iy,float w,float h,string path,float height,LootTableDefinition loot,float duration=3.5f) =>
        new(id,name,room,new Vector2(x,y),new Vector2(ix,iy),new Rect2(x-w*.5f,y-h*.5f,w,h),Root+path,height,loot,duration);
    private static BedDefinition B(string id,string name,string room,float x,float y,float ix,float iy,float w,float h,string path,float height) =>
        new(id,name,room,new Vector2(x,y),new Vector2(ix,iy),new Rect2(x-w*.5f,y-h*.5f,w,h),Root+path,height);
}
