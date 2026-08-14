#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using AshwoodCounty.World;
using Godot;

namespace AshwoodCounty.Authoring;

public enum RoadSmoothingMode { Linear, CatmullRom }

public sealed record RoadProfileDefinition(
    string Id,
    float DefaultWidth,
    RoadSmoothingMode Smoothing,
    float SamplesPerGridUnit,
    float ShoulderScale,
    Color Surface,
    Color Shoulder,
    string SurfaceTexture,
    RoadMarkingStyle Markings,
    float SnapTolerance,
    float MinimumTurnRadius);

public enum RoadMarkingStyle { None, SingleCenter, DoubleCenter, Highway, FarmRuts, LoggingRuts }

/// <summary>Single source of road-type behavior for editor, runtime and validation.</summary>
public static class RoadProfiles
{
    private const string Asphalt = "res://assets/art/roads/materials/asphalt_surface.png";
    private const string WornAsphalt = "res://assets/art/roads/materials/asphalt_worn_surface.png";
    private const string Dirt = "res://assets/art/roads/materials/dirt_surface.png";
    private const string Mud = "res://assets/art/roads/materials/mud_surface.png";

    public static readonly IReadOnlyList<RoadProfileDefinition> All =
    [
        new("Highway",2.8f,RoadSmoothingMode.CatmullRom,3.2f,1.34f,new("4b4f50"),new("69645b"),Asphalt,RoadMarkingStyle.Highway,.9f,4.5f),
        new("Town Road",1.8f,RoadSmoothingMode.Linear,2.4f,1.25f,new("505353"),new("716b61"),Asphalt,RoadMarkingStyle.DoubleCenter,.6f,1.2f),
        new("County Road",2.2f,RoadSmoothingMode.CatmullRom,2.8f,1.3f,new("535656"),new("756e62"),WornAsphalt,RoadMarkingStyle.DoubleCenter,.75f,3f),
        new("Rural Asphalt Road",1.55f,RoadSmoothingMode.CatmullRom,2.5f,1.28f,new("555958"),new("746b5d"),WornAsphalt,RoadMarkingStyle.SingleCenter,.65f,2.2f),
        new("Dirt Road",1.35f,RoadSmoothingMode.CatmullRom,2.25f,1.36f,new("967650"),new("6e6049"),Dirt,RoadMarkingStyle.None,.6f,1.8f),
        new("Farm Track",.95f,RoadSmoothingMode.CatmullRom,2f,1.42f,new("8f724e"),new("5e5542"),Dirt,RoadMarkingStyle.FarmRuts,.5f,1.4f),
        new("Logging Road",1.45f,RoadSmoothingMode.CatmullRom,2.2f,1.42f,new("765d43"),new("514838"),Mud,RoadMarkingStyle.LoggingRuts,.65f,2f),
        new("Footpath",.42f,RoadSmoothingMode.CatmullRom,1.8f,1.55f,new("987b56"),new("66704d"),Dirt,RoadMarkingStyle.None,.35f,.6f)
    ];

    public static RoadProfileDefinition Get(string? id)
    {
        string normalized = Normalize(id);
        return All.FirstOrDefault(profile => profile.Id == normalized) ?? All[3];
    }

    public static string Normalize(string? id) => id switch
    {
        "Paved Town Road" => "Town Road",
        "Rural Road" => "Rural Asphalt Road",
        "Forest Track" => "Logging Road",
        _ when string.IsNullOrWhiteSpace(id) => "Rural Asphalt Road",
        _ => id!
    };
}

public readonly record struct RoadSplineSample(Vector2 GridPosition, Vector2 CanvasPosition, Vector2 CanvasTangent, float Distance, float Width, int ControlSegment);

public static class RoadSplineGeometry
{
    public static IReadOnlyList<RoadSplineSample> Sample(AuthoredPathData path)
    {
        if(path.Points.Count < 2) return [];
        RoadProfileDefinition profile = RoadProfiles.Get(path.PathType);
        List<RoadSplineSample> result = [];
        float distance = 0;
        Vector2 previousCanvas = Vector2.Zero;
        for(int segment = 0; segment < path.Points.Count - 1; segment++)
        {
            Vector2 p0 = path.Points[Mathf.Max(0,segment - 1)].Vector;
            Vector2 p1 = path.Points[segment].Vector;
            Vector2 p2 = path.Points[segment + 1].Vector;
            Vector2 p3 = path.Points[Mathf.Min(path.Points.Count - 1,segment + 2)].Vector;
            int steps = Mathf.Clamp(Mathf.CeilToInt(p1.DistanceTo(p2) * profile.SamplesPerGridUnit),2,96);
            for(int step = segment == 0 ? 0 : 1; step <= steps; step++)
            {
                float t = step / (float)steps;
                Vector2 grid = profile.Smoothing == RoadSmoothingMode.Linear ? p1.Lerp(p2,t) : CatmullRom(p0,p1,p2,p3,t);
                Vector2 canvas = IsometricGrid.GridToScreen(grid);
                if(result.Count > 0) distance += previousCanvas.DistanceTo(canvas);
                float width = Mathf.Lerp(Mathf.Max(.1f,path.Width * path.Points[segment].WidthScale),Mathf.Max(.1f,path.Width * path.Points[segment + 1].WidthScale),t);
                result.Add(new(grid,canvas,Vector2.Zero,distance,width,segment));
                previousCanvas = canvas;
            }
        }
        for(int i = 0; i < result.Count; i++)
        {
            Vector2 before = result[Mathf.Max(0,i - 1)].CanvasPosition;
            Vector2 after = result[Mathf.Min(result.Count - 1,i + 1)].CanvasPosition;
            Vector2 tangent = (after - before).Normalized();
            result[i] = result[i] with { CanvasTangent = tangent.IsZeroApprox() ? Vector2.Right : tangent };
        }
        return result;
    }

    public static Vector2 ClosestPoint(AuthoredPathData path,Vector2 point,out int controlSegment,out float distance)
    {
        IReadOnlyList<RoadSplineSample> samples = Sample(path);
        Vector2 closest = point; controlSegment = -1; distance = float.PositiveInfinity;
        for(int i = 0; i < samples.Count - 1; i++)
        {
            Vector2 candidate = ClosestOnSegment(point,samples[i].GridPosition,samples[i + 1].GridPosition);
            float candidateDistance = candidate.DistanceTo(point);
            if(candidateDistance >= distance) continue;
            distance = candidateDistance; closest = candidate; controlSegment = samples[i].ControlSegment;
        }
        return closest;
    }

    public static float ApproximateLength(AuthoredPathData path)
    {
        IReadOnlyList<RoadSplineSample> samples = Sample(path);
        return samples.Count == 0 ? 0 : samples[^1].Distance / IsometricGrid.TileHeight;
    }

    public static int InsertControlPoint(AuthoredPathData path,Vector2 point,float tolerance=.65f)
    {
        Vector2 closest=ClosestPoint(path,point,out int segment,out float distance);if(segment<0||distance>tolerance)return -1;path.Points.Insert(segment+1,AuthoredPointData.From(closest));return segment+1;
    }

    public static bool RemoveControlPoint(AuthoredPathData path,int index)
    {
        if(path.Points.Count<=2||index<0||index>=path.Points.Count)return false;path.Points.RemoveAt(index);return true;
    }

    public static bool TrySegmentIntersection(Vector2 a,Vector2 b,Vector2 c,Vector2 d,out Vector2 intersection,bool includeEnds=false)
    {
        intersection=Vector2.Zero;Vector2 r=b-a,s=d-c;float cross=r.Cross(s);if(Mathf.Abs(cross)<.0001f)return false;float t=(c-a).Cross(s)/cross,u=(c-a).Cross(r)/cross;float margin=includeEnds?-.001f:.02f;if(t<margin||t>1-margin||u<margin||u>1-margin)return false;intersection=a+r*t;return true;
    }

    private static Vector2 CatmullRom(Vector2 p0,Vector2 p1,Vector2 p2,Vector2 p3,float t)
    {
        float t2=t*t,t3=t2*t;
        return .5f*((2*p1)+(-p0+p2)*t+(2*p0-5*p1+4*p2-p3)*t2+(-p0+3*p1-3*p2+p3)*t3);
    }

    private static Vector2 ClosestOnSegment(Vector2 point,Vector2 a,Vector2 b)
    {
        Vector2 line=b-a;float length=line.LengthSquared();if(length<.00001f)return a;
        return a+line*Mathf.Clamp((point-a).Dot(line)/length,0,1);
    }
}

public sealed record RoadGraphNode(string Id,Vector2 Position,IReadOnlyList<string> PathIds,bool BridgeSocket,int ConnectionCount=0,string Topology="Endpoint")
{
    public int Degree => ConnectionCount;
    public string JunctionKind => Topology;
}
public sealed record RoadGraphEdge(string PathId,string StartNodeId,string EndNodeId,float Length,string Profile);
public sealed record BridgeSocket(string Id,Vector2 Position,string ObjectId,float SupportedWidth,IReadOnlyList<string> SupportedProfiles);

/// <summary>Authoritative query hook for later routing, traffic and road-aware gameplay.</summary>
public sealed class RoadNetworkGraph
{
    public IReadOnlyList<RoadGraphNode> Nodes { get; init; }=[];
    public IReadOnlyList<RoadGraphEdge> Edges { get; init; }=[];
    public RoadGraphNode? NearestNode(Vector2 position,float maxDistance=2) => Nodes.Where(node=>node.Position.DistanceTo(position)<=maxDistance).MinBy(node=>node.Position.DistanceTo(position));

    public static RoadNetworkGraph Build(AuthoredCountyDocument document)
    {
        List<BridgeSocket> sockets=BridgeSockets(document).ToList();
        List<(string PathId,Vector2 Position,bool Bridge)> endpointCandidates=[];
        foreach(AuthoredPathData path in document.Paths.Where(path=>path.LineKind=="Road"&&path.Points.Count>1))
        {
            endpointCandidates.Add((path.Id,path.Points[0].Vector,false));endpointCandidates.Add((path.Id,path.Points[^1].Vector,false));
        }
        AuthoredPathData[] roads=document.Paths.Where(path=>path.LineKind=="Road"&&path.Points.Count>1).ToArray();
        for(int a=0;a<roads.Length;a++)for(int b=a+1;b<roads.Length;b++)
        {
            Rect2 boundsA=Bounds(roads[a].Points.Select(point=>point.Vector)).Grow(1),boundsB=Bounds(roads[b].Points.Select(point=>point.Vector)).Grow(1);if(!boundsA.Intersects(boundsB))continue;
            IReadOnlyList<RoadSplineSample> sa=RoadSplineGeometry.Sample(roads[a]),sb=RoadSplineGeometry.Sample(roads[b]);
            for(int ia=0;ia<sa.Count-1;ia++)for(int ib=0;ib<sb.Count-1;ib++){if(!Bounds([sa[ia].GridPosition,sa[ia+1].GridPosition]).Grow(.02f).Intersects(Bounds([sb[ib].GridPosition,sb[ib+1].GridPosition]).Grow(.02f)))continue;if(RoadSplineGeometry.TrySegmentIntersection(sa[ia].GridPosition,sa[ia+1].GridPosition,sb[ib].GridPosition,sb[ib+1].GridPosition,out Vector2 intersection))
            {
                if(endpointCandidates.Any(item=>item.PathId==roads[a].Id&&item.Position.DistanceTo(intersection)<.15f))continue;
                endpointCandidates.Add((roads[a].Id,intersection,false));endpointCandidates.Add((roads[b].Id,intersection,false));
            }}
        }
        endpointCandidates.AddRange(sockets.Select(socket=>(socket.Id,socket.Position,true)));
        List<List<(string PathId,Vector2 Position,bool Bridge)>> clusters=[];
        foreach(var candidate in endpointCandidates)
        {
            float tolerance=candidate.Bridge?.85f:RoadProfiles.Get(document.Paths.FirstOrDefault(path=>path.Id==candidate.PathId)?.PathType).SnapTolerance;
            List<(string PathId,Vector2 Position,bool Bridge)>? cluster=clusters.FirstOrDefault(group=>group.Any(item=>item.Position.DistanceTo(candidate.Position)<=tolerance));
            if(cluster is null){cluster=[];clusters.Add(cluster);}cluster.Add(candidate);
        }
        List<RoadGraphNode> nodes=[];
        foreach((List<(string PathId,Vector2 Position,bool Bridge)> cluster,int index) in clusters.Select((cluster,index)=>(cluster,index)))
        {
            Vector2 position=cluster.Select(item=>item.Position).Aggregate(Vector2.Zero,(sum,p)=>sum+p)/cluster.Count;
            nodes.Add(new($"road_node_{index:0000}",position,cluster.Where(item=>!item.Bridge).Select(item=>item.PathId).Distinct().ToArray(),cluster.Any(item=>item.Bridge)));
        }
        List<RoadGraphEdge> edges=[];
        foreach(AuthoredPathData path in roads)
        {
            IReadOnlyList<RoadSplineSample> samples=RoadSplineGeometry.Sample(path);List<(RoadGraphNode Node,float Distance)> pathNodes=[];
            foreach(RoadGraphNode node in nodes.Where(node=>node.PathIds.Contains(path.Id))){RoadSplineGeometry.ClosestPoint(path,node.Position,out _,out _);float along=samples.OrderBy(sample=>sample.GridPosition.DistanceSquaredTo(node.Position)).First().Distance;pathNodes.Add((node,along));}
            pathNodes=pathNodes.OrderBy(item=>item.Distance).ToList();for(int i=0;i<pathNodes.Count-1;i++)if(pathNodes[i].Node.Id!=pathNodes[i+1].Node.Id)edges.Add(new(path.Id,pathNodes[i].Node.Id,pathNodes[i+1].Node.Id,(pathNodes[i+1].Distance-pathNodes[i].Distance)/IsometricGrid.TileHeight,RoadProfiles.Get(path.PathType).Id));
        }
        Dictionary<string,RoadGraphNode> lookup=nodes.ToDictionary(node=>node.Id);nodes=nodes.Select(node=>{RoadGraphEdge[] incident=edges.Where(edge=>edge.StartNodeId==node.Id||edge.EndNodeId==node.Id).ToArray();Vector2[] directions=incident.Select(edge=>lookup[edge.StartNodeId==node.Id?edge.EndNodeId:edge.StartNodeId].Position-node.Position).Where(direction=>!direction.IsZeroApprox()).Select(direction=>direction.Normalized()).ToArray();return node with{ConnectionCount=incident.Length,Topology=ClassifyTopology(directions)};}).ToList();return new(){Nodes=nodes,Edges=edges};
    }

    public static IEnumerable<BridgeSocket> BridgeSockets(AuthoredCountyDocument document)
    {
        foreach(AuthoredWorldObjectData item in document.WorldObjects.Where(item=>item.Category.Contains("Bridge",StringComparison.OrdinalIgnoreCase)||item.AssetPath.Contains("/bridges/")))
        {
            (float supportedWidth,IReadOnlyList<string> profiles)=BridgeCapabilities(item.AssetPath);
            Vector2 canvasDirection=Vector2.Right.Rotated(Mathf.DegToRad(item.RotationDegrees));float halfPixels=item.Scale*150;
            if(ResourceLoader.Exists(item.AssetPath))halfPixels=TextureRegistry.Get(item.AssetPath).GetWidth()*Mathf.Max(.02f,item.Scale)*.46f;
            Vector2 gridOffset=IsometricGrid.ScreenToGrid(canvasDirection*halfPixels);float length=Mathf.Clamp(gridOffset.Length(),1,8);gridOffset=gridOffset.Normalized()*length;Vector2 center=new(item.X,item.Y);
            yield return new($"bridge_{item.Id}_a",center-gridOffset,item.Id,supportedWidth,profiles);
            yield return new($"bridge_{item.Id}_b",center+gridOffset,item.Id,supportedWidth,profiles);
        }
    }

    private static Rect2 Bounds(IEnumerable<Vector2> points){Vector2[] p=points.ToArray();float minX=p.Min(v=>v.X),minY=p.Min(v=>v.Y),maxX=p.Max(v=>v.X),maxY=p.Max(v=>v.Y);return new(new Vector2(minX,minY),new Vector2(maxX-minX,maxY-minY));}
    private static string ClassifyTopology(Vector2[] directions)
    {
        if(directions.Length<=1)return "Endpoint";if(directions.Length==2)return directions[0].Dot(directions[1])<-.86f?"Continuation":"Bend";if(directions.Length==3){for(int a=0;a<3;a++)for(int b=a+1;b<3;b++)if(directions[a].Dot(directions[b])<-.82f)return "T Junction";return "Y Junction";}if(directions.Length==4)return "Crossroad";return "Complex Junction";
    }
    private static (float Width,IReadOnlyList<string> Profiles) BridgeCapabilities(string path)
    {
        if(path.Contains("footbridge"))return(.7f,["Footpath"]);if(path.Contains("timber_road"))return(1.6f,["Dirt Road","Farm Track","Logging Road","County Road"]);if(path.Contains("highway"))return(3.2f,["Highway","County Road","Town Road"]);if(path.Contains("culvert"))return(2.2f,["County Road","Rural Asphalt Road","Dirt Road"]);return(2.4f,["County Road","Rural Asphalt Road","Town Road","Dirt Road"]);
    }
}
