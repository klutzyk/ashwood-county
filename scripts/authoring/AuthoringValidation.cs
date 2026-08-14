#nullable enable

using System.Collections.Generic;
using System.Linq;
using AshwoodCounty.Buildings.Interiors;
using AshwoodCounty.World.County;
using Godot;

namespace AshwoodCounty.Authoring;

public enum AuthoringValidationSeverity { Valid,Warning,Invalid }
public sealed record AuthoringValidationIssue(AuthoringValidationSeverity Severity,string Message,Vector2 Position);

public static class AuthoringValidation
{
    public static IReadOnlyList<AuthoringValidationIssue> ValidateWorld(AuthoredCountyDocument document)
    {
        List<AuthoringValidationIssue> issues=[];IEnumerable<(string Id,Vector2 Position,string Kind)> identities=document.WorldObjects.Select(item=>(item.Id,new Vector2(item.X,item.Y),"object")).Concat(document.Buildings.Select(item=>(item.Id,new Vector2(item.ExteriorX,item.ExteriorY),"building"))).Concat(document.Paths.Select(item=>(item.Id,item.Points.FirstOrDefault()?.Vector??Vector2.Zero,"road"))).Concat(document.TerrainStamps.Select(item=>(item.Id,new Vector2(item.X,item.Y),"terrain")));
        foreach(IGrouping<string,(string Id,Vector2 Position,string Kind)> group in identities.Where(item=>!string.IsNullOrWhiteSpace(item.Id)).GroupBy(item=>item.Id).Where(group=>group.Count()>1))issues.Add(new(AuthoringValidationSeverity.Invalid,$"Duplicate stable ID '{group.Key}'.",group.First().Position));
        foreach(AuthoredWorldObjectData item in document.WorldObjects){Vector2 position=new(item.X,item.Y);if(!CountyCoordinateSpace.GridBounds.HasPoint(position))issues.Add(new(AuthoringValidationSeverity.Invalid,$"{item.DisplayName} is outside the county.",position));if(string.IsNullOrWhiteSpace(item.AssetPath)||!ResourceLoader.Exists(item.AssetPath))issues.Add(new(AuthoringValidationSeverity.Invalid,$"{item.DisplayName} references a missing asset.",position));}
        foreach(AuthoredBuildingData item in document.Buildings){Vector2 position=new(item.ExteriorX,item.ExteriorY);if(!CountyCoordinateSpace.GridBounds.HasPoint(position))issues.Add(new(AuthoringValidationSeverity.Invalid,$"{item.DisplayName} is outside the county.",position));if(string.IsNullOrWhiteSpace(item.ExteriorAssetPath)||!ResourceLoader.Exists(item.ExteriorAssetPath))issues.Add(new(AuthoringValidationSeverity.Invalid,$"{item.DisplayName} references a missing exterior asset.",position));if(item.Doors.Count==0)issues.Add(new(AuthoringValidationSeverity.Warning,$"{item.DisplayName} has no authored entrance.",position));}
        foreach(AuthoredPathData path in document.Paths){Vector2 position=path.Points.FirstOrDefault()?.Vector??Vector2.Zero;if(path.Points.Count<2)issues.Add(new(AuthoringValidationSeverity.Invalid,$"Road '{path.DisplayName}' has fewer than two control points.",position));if(path.Width<=0)issues.Add(new(AuthoringValidationSeverity.Invalid,$"Road '{path.DisplayName}' has invalid width.",position));if(path.Points.Any(point=>!CountyCoordinateSpace.GridBounds.HasPoint(point.Vector)))issues.Add(new(AuthoringValidationSeverity.Invalid,$"Road '{path.DisplayName}' leaves the county bounds.",position));}
        foreach(AuthoredTerrainStampData item in document.TerrainStamps)if(string.IsNullOrWhiteSpace(item.AssetPath)||!ResourceLoader.Exists(item.AssetPath))issues.Add(new(AuthoringValidationSeverity.Invalid,"Painted terrain references a missing asset.",new(item.X,item.Y)));
        if(issues.Count==0)issues.Add(new(AuthoringValidationSeverity.Valid,$"World authoring data is valid ({document.WorldObjects.Count} objects, {document.TerrainStamps.Count} paint stamps, {document.Paths.Count} roads).",Vector2.Zero));return issues;
    }

    public static IReadOnlyList<AuthoringValidationIssue> Validate(AuthoredBuildingData authored)
    {
        List<AuthoringValidationIssue> issues=[];
        InteriorBuildingDefinition definition=AuthoredInteriorConverter.Convert(authored);
        Rect2 footprint=definition.Footprint;
        foreach(RoomDefinition room in definition.Rooms)
            if(!ContainsRect(footprint,room.Bounds))issues.Add(new(AuthoringValidationSeverity.Invalid,$"Room '{room.DisplayName}' extends outside the building.",room.Bounds.GetCenter()));
        foreach(FurnitureDefinition item in definition.Furniture)
        {
            if(!footprint.HasPoint(item.Position))issues.Add(new(AuthoringValidationSeverity.Invalid,$"{item.DisplayName} is outside the footprint.",item.Position));
            if(definition.Walls.Any(wall=>DistanceToSegment(item.Position,wall.Start,wall.End)<.16f))issues.Add(new(AuthoringValidationSeverity.Warning,$"{item.DisplayName} overlaps a wall.",item.Position));
            if(item.TargetHeight<20||item.TargetHeight>260)issues.Add(new(AuthoringValidationSeverity.Warning,$"{item.DisplayName} has an extreme visual scale.",item.Position));
            if(item.BlocksMovement&&definition.Furniture.Any(other=>other.Id!=item.Id&&other.BlocksMovement&&item.Footprint.Intersects(other.Footprint)))issues.Add(new(AuthoringValidationSeverity.Invalid,$"{item.DisplayName} overlaps blocking furniture.",item.Position));
        }
        foreach(ContainerDefinition item in definition.Containers)
        {
            if(!footprint.HasPoint(item.Position))issues.Add(new(AuthoringValidationSeverity.Invalid,$"{item.DisplayName} is outside the footprint.",item.Position));
            if(!InteriorNavigationService.CanReach(definition,NearestEntrance(definition),item.InteractionPosition))issues.Add(new(AuthoringValidationSeverity.Invalid,$"{item.DisplayName} interaction point is unreachable.",item.InteractionPosition));
        }
        foreach(BedDefinition item in definition.Beds)
            if(!InteriorNavigationService.CanReach(definition,NearestEntrance(definition),item.InteractionPosition))issues.Add(new(AuthoringValidationSeverity.Invalid,$"{item.DisplayName} interaction point is unreachable.",item.InteractionPosition));
        DoorDefinition[] entrances=definition.Doors.Where(door=>door.Exterior).ToArray();
        if(entrances.Length==0)issues.Add(new(AuthoringValidationSeverity.Invalid,"Building has no exterior entrance.",footprint.GetCenter()));
        foreach(DoorDefinition door in definition.Doors)
        {
            if(door.OutsideApproachPoint.IsZeroApprox()||door.InsideArrivalPoint.IsZeroApprox())issues.Add(new(AuthoringValidationSeverity.Invalid,$"{door.DisplayName} lacks deterministic approach/arrival points.",door.Position));
            if(definition.Walls.All(wall=>DistanceToSegment(door.Position,wall.Start,wall.End)>.7f))issues.Add(new(AuthoringValidationSeverity.Warning,$"{door.DisplayName} is not attached to a wall opening.",door.Position));
            if(!door.Exterior&&!footprint.HasPoint(door.Position))issues.Add(new(AuthoringValidationSeverity.Invalid,$"{door.DisplayName} lies outside the building.",door.Position));
        }
        foreach(RoomDefinition room in definition.Rooms)
            if(!RoomHasReachablePoint(definition,room,NearestEntrance(definition)))issues.Add(new(AuthoringValidationSeverity.Invalid,$"{room.DisplayName} is inaccessible.",room.Bounds.GetCenter()));
        if(issues.Count==0)issues.Add(new(AuthoringValidationSeverity.Valid,"Building geometry, entrance, rooms, and interactions are valid.",footprint.GetCenter()));
        return issues;
    }

    public static bool TestEntrance(AuthoredBuildingData authored,out IReadOnlyList<Vector2> route,out string detail)
    {
        InteriorBuildingDefinition definition=AuthoredInteriorConverter.Convert(authored);
        DoorDefinition? entrance=definition.Doors.FirstOrDefault(door=>door.Exterior);
        if(entrance is null){route=[];detail="FAIL: no exterior entrance";return false;}
        IReadOnlyList<Vector2> inside=InteriorNavigationService.PlanDefinition(definition,entrance.InsideArrivalPoint,definition.Rooms.FirstOrDefault()?.Bounds.GetCenter()??definition.Footprint.GetCenter());
        bool pass=!entrance.OutsideApproachPoint.IsZeroApprox()&&!entrance.InsideArrivalPoint.IsZeroApprox()&&inside.Count>0&&inside[^1].DistanceTo(definition.Rooms.FirstOrDefault()?.Bounds.GetCenter()??definition.Footprint.GetCenter())<.35f;
        route=[entrance.OutsideApproachPoint,entrance.Position,entrance.InsideArrivalPoint,..inside.Skip(1)];
        detail=pass?"PASS: outside approach -> door -> inside arrival -> first room":"FAIL: entrance route is blocked";
        return pass;
    }

    private static Vector2 NearestEntrance(InteriorBuildingDefinition definition)=>definition.Doors.FirstOrDefault(door=>door.Exterior)?.InsideArrivalPoint??definition.Footprint.GetCenter();
    private static bool RoomHasReachablePoint(InteriorBuildingDefinition definition,RoomDefinition room,Vector2 start)
    {
        for(int y=1;y<=3;y++)for(int x=1;x<=3;x++)
        {
            Vector2 point=room.Bounds.Position+new Vector2(room.Bounds.Size.X*x/4f,room.Bounds.Size.Y*y/4f);
            if(InteriorNavigationService.CanReach(definition,start,point))return true;
        }
        return false;
    }
    private static bool ContainsRect(Rect2 outer,Rect2 inner)=>outer.HasPoint(inner.Position)&&inner.End.X<=outer.End.X&&inner.End.Y<=outer.End.Y;
    private static float DistanceToSegment(Vector2 point,Vector2 start,Vector2 end){Vector2 line=end-start;float length=line.LengthSquared();if(length<.0001f)return point.DistanceTo(start);float t=Mathf.Clamp((point-start).Dot(line)/length,0,1);return point.DistanceTo(start+line*t);}
}
