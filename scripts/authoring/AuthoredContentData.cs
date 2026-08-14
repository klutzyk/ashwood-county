#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using AshwoodCounty.Buildings.Interiors;
using AshwoodCounty.Resources;
using Godot;

namespace AshwoodCounty.Authoring;

public sealed class AuthoredCountyDocument
{
    public int FormatVersion { get; set; } = 1;
    public List<AuthoredWorldObjectData> WorldObjects { get; set; } = [];
    public List<AuthoredBuildingData> Buildings { get; set; } = [];
}

public sealed class AuthoredWorldObjectData
{
    public string Id { get; set; } = string.Empty;
    public string DisplayName { get; set; } = "Authored Object";
    public string AssetPath { get; set; } = string.Empty;
    public string Category { get; set; } = "Decoration";
    public string GameplayType { get; set; } = "Decoration";
    public float X { get; set; }
    public float Y { get; set; }
    public float Scale { get; set; } = 1;
    public float ScaleY { get; set; }
    public float RotationDegrees { get; set; }
    public float AnchorX { get; set; } = .5f;
    public float AnchorY { get; set; } = 1;
    public bool Collision { get; set; }
}

public sealed class AuthoredBuildingData
{
    public string Id { get; set; } = string.Empty;
    public string DisplayName { get; set; } = "Authored Building";
    public string ExteriorAssetPath { get; set; } = string.Empty;
    public float ExteriorX { get; set; }
    public float ExteriorY { get; set; }
    public float ExteriorTargetHeight { get; set; } = 420;
    public float ExteriorTargetWidth { get; set; }
    public float ExteriorRotationDegrees { get; set; }
    public float FootprintX { get; set; }
    public float FootprintY { get; set; }
    public float FootprintWidth { get; set; } = 8;
    public float FootprintHeight { get; set; } = 7;
    public List<AuthoredRoomData> Rooms { get; set; } = [];
    public List<AuthoredWallData> Walls { get; set; } = [];
    public List<AuthoredDoorData> Doors { get; set; } = [];
    public List<AuthoredFurnitureData> Furniture { get; set; } = [];
    public List<AuthoredContainerData> Containers { get; set; } = [];
    public List<AuthoredBedData> Beds { get; set; } = [];
}

public sealed class AuthoredRoomData
{
    public string Id { get; set; } = string.Empty;
    public string DisplayName { get; set; } = "Room";
    public float X { get; set; }
    public float Y { get; set; }
    public float Width { get; set; }
    public float Height { get; set; }
    public string FloorTexturePath { get; set; } = string.Empty;
    public string FloorTint { get; set; } = "b5a17d";
}

public sealed class AuthoredWallData
{
    public string Id { get; set; } = string.Empty;
    public float StartX { get; set; }
    public float StartY { get; set; }
    public float EndX { get; set; }
    public float EndY { get; set; }
    public string TexturePath { get; set; } = string.Empty;
    public bool FlipVisual { get; set; }
}

public sealed class AuthoredDoorData
{
    public string Id { get; set; } = string.Empty;
    public string DisplayName { get; set; } = "Door";
    public string WallId { get; set; } = string.Empty;
    public string RoomAId { get; set; } = string.Empty;
    public string RoomBId { get; set; } = string.Empty;
    public bool Exterior { get; set; }
    public float X { get; set; }
    public float Y { get; set; }
    public float OutsideApproachX { get; set; }
    public float OutsideApproachY { get; set; }
    public float InsideArrivalX { get; set; }
    public float InsideArrivalY { get; set; }
    public string ClosedTexturePath { get; set; } = string.Empty;
    public string OpenTexturePath { get; set; } = string.Empty;
    public string InitialState { get; set; } = "Closed";
}

public sealed class AuthoredFurnitureData
{
    public string Id { get; set; } = string.Empty;
    public string DisplayName { get; set; } = "Furniture";
    public string RoomId { get; set; } = string.Empty;
    public string AssetPath { get; set; } = string.Empty;
    public float X { get; set; }
    public float Y { get; set; }
    public float Width { get; set; } = .5f;
    public float Height { get; set; } = .5f;
    public float TargetHeight { get; set; } = 72;
    public bool BlocksMovement { get; set; } = true;
    public string Tint { get; set; } = "ffffff";
}

public sealed class AuthoredContainerData
{
    public string Id { get; set; } = string.Empty;
    public string DisplayName { get; set; } = "Container";
    public string RoomId { get; set; } = string.Empty;
    public string AssetPath { get; set; } = string.Empty;
    public string LootPreset { get; set; } = "Bedroom Storage";
    public float X { get; set; }
    public float Y { get; set; }
    public float InteractionX { get; set; }
    public float InteractionY { get; set; }
    public float Width { get; set; } = .5f;
    public float Height { get; set; } = .5f;
    public float TargetHeight { get; set; } = 72;
    public float SearchDuration { get; set; } = 3.5f;
}

public sealed class AuthoredBedData
{
    public string Id { get; set; } = string.Empty;
    public string DisplayName { get; set; } = "Bed";
    public string RoomId { get; set; } = string.Empty;
    public string AssetPath { get; set; } = string.Empty;
    public float X { get; set; }
    public float Y { get; set; }
    public float InteractionX { get; set; }
    public float InteractionY { get; set; }
    public float Width { get; set; } = 1.4f;
    public float Height { get; set; } = .8f;
    public float TargetHeight { get; set; } = 90;
}

public static class AuthoredContentRepository
{
    public const string ResourcePath = "res://data/authoring/ashwood_county.authored.json";
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public static AuthoredCountyDocument Load()
    {
        if (!Godot.FileAccess.FileExists(ResourcePath)) return new AuthoredCountyDocument();
        string json = Godot.FileAccess.GetFileAsString(ResourcePath);
        AuthoredCountyDocument? document = JsonSerializer.Deserialize<AuthoredCountyDocument>(json, Options);
        return document ?? new AuthoredCountyDocument();
    }

    public static void Save(AuthoredCountyDocument document)
    {
        string absolute = ProjectSettings.GlobalizePath(ResourcePath);
        Directory.CreateDirectory(Path.GetDirectoryName(absolute)!);
        string temporary = absolute + ".tmp";
        File.WriteAllText(temporary, Serialize(document));
        File.Move(temporary, absolute, true);
    }

    public static string Serialize(AuthoredCountyDocument document) => JsonSerializer.Serialize(document, Options);
    public static AuthoredCountyDocument Deserialize(string json) =>
        JsonSerializer.Deserialize<AuthoredCountyDocument>(json, Options) ?? new AuthoredCountyDocument();
}

public static class AuthoredInteriorConverter
{
    private static readonly Dictionary<string, LootTableDefinition> LootPresets = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Kitchen Refrigerator"] = new("kitchen_refrigerator",2,new(ResourceType.Food,1,3,.63f),new(ResourceType.Medicine,1,1,.08f),new(null,0,0,.29f)),
        ["Kitchen Cupboard"] = new("kitchen_cupboard",2,new(ResourceType.Food,1,2,.48f),new(ResourceType.Materials,1,2,.25f),new(null,0,0,.27f)),
        ["Bathroom Cabinet"] = new("bathroom_cabinet",2,new(ResourceType.Medicine,1,2,.34f),new(ResourceType.Materials,1,1,.18f),new(null,0,0,.48f)),
        ["Bedroom Storage"] = new("bedroom_storage",2,new(ResourceType.Materials,1,2,.37f),new(ResourceType.Medicine,1,1,.10f),new(ResourceType.Food,1,1,.08f),new(null,0,0,.45f)),
        ["Garage Shelf"] = new("garage_shelf",3,new(ResourceType.Materials,1,3,.62f),new(ResourceType.Medicine,1,1,.06f),new(null,0,0,.32f))
    };

    public static IReadOnlyList<string> LootPresetNames => LootPresets.Keys.OrderBy(name => name).ToArray();

    public static InteriorBuildingDefinition Convert(AuthoredBuildingData source) => new(
        source.Id, source.DisplayName, new Vector2(source.ExteriorX,source.ExteriorY),
        new Rect2(source.FootprintX,source.FootprintY,source.FootprintWidth,source.FootprintHeight),
        source.ExteriorAssetPath,source.ExteriorTargetHeight,source.ExteriorTargetWidth,source.ExteriorRotationDegrees,
        source.Rooms.Select(room=>new RoomDefinition(room.Id,room.DisplayName,new Rect2(room.X,room.Y,room.Width,room.Height),room.FloorTexturePath,ParseColor(room.FloorTint))).ToArray(),
        source.Walls.Select(wall=>new WallDefinition(wall.Start(),wall.End(),wall.TexturePath,wall.FlipVisual)).ToArray(),
        source.Doors.Select(door=>new DoorDefinition(door.Id,door.DisplayName,new Vector2(door.X,door.Y),door.RoomAId,door.RoomBId,door.Exterior,door.ClosedTexturePath,door.OpenTexturePath,ParseDoorState(door.InitialState),new Vector2(door.OutsideApproachX,door.OutsideApproachY),new Vector2(door.InsideArrivalX,door.InsideArrivalY),door.WallId)).ToArray(),
        source.Furniture.Select(item=>new FurnitureDefinition(item.Id,item.DisplayName,new Vector2(item.X,item.Y),Centered(item.X,item.Y,item.Width,item.Height),item.AssetPath,item.TargetHeight,item.BlocksMovement,ParseColor(item.Tint))).ToArray(),
        source.Containers.Select(item=>new ContainerDefinition(item.Id,item.DisplayName,item.RoomId,new Vector2(item.X,item.Y),new Vector2(item.InteractionX,item.InteractionY),Centered(item.X,item.Y,item.Width,item.Height),item.AssetPath,item.TargetHeight,GetLoot(item.LootPreset),item.SearchDuration)).ToArray(),
        source.Beds.Select(item=>new BedDefinition(item.Id,item.DisplayName,item.RoomId,new Vector2(item.X,item.Y),new Vector2(item.InteractionX,item.InteractionY),Centered(item.X,item.Y,item.Width,item.Height),item.AssetPath,item.TargetHeight)).ToArray());

    private static Rect2 Centered(float x,float y,float width,float height)=>new(x-width*.5f,y-height*.5f,width,height);
    private static Color ParseColor(string value)=>Color.FromHtml(string.IsNullOrWhiteSpace(value)?"ffffff":value);
    private static InteriorDoorState ParseDoorState(string value)=>Enum.TryParse(value,true,out InteriorDoorState state)?state:InteriorDoorState.Closed;
    private static LootTableDefinition GetLoot(string name)=>LootPresets.GetValueOrDefault(name,LootPresets["Bedroom Storage"]);
    private static Vector2 Start(this AuthoredWallData wall)=>new(wall.StartX,wall.StartY);
    private static Vector2 End(this AuthoredWallData wall)=>new(wall.EndX,wall.EndY);
}
