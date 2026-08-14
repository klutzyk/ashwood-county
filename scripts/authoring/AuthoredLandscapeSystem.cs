#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using AshwoodCounty.World;
using AshwoodCounty.World.County;
using Godot;

namespace AshwoodCounty.Authoring;

/// <summary>Chunk-streamed renderer shared by the Studio and normal gameplay.</summary>
public partial class AuthoredLandscapeSystem : Node
{
    private readonly Dictionary<Vector2I,AuthoredTerrainChunkVisual> _active=[];
    private CountyWorld _county=null!;
    private IsometricWorld _world=null!;
    private AuthoredCountyDocument _document=null!;
    private bool _initialized;
    private readonly Dictionary<string,AuthoredPathVisual> _paths=[];
    private RoadJunctionVisual? _junctions;
    private bool _terrainVisible=true,_roadsVisible=true;

    public void Initialize(CountyWorld county,IsometricWorld world,AuthoredCountyDocument document){_county=county;_world=world;_document=document;_initialized=true;}

    public override void _Ready()
    {
        if(!_initialized){_county=GetNode<CountyWorld>("../World/CountyWorld");_world=GetNode<IsometricWorld>("../World");_document=AuthoredContentRepository.Load();}
        _county.ChunkLoaded+=LoadChunk;_county.ChunkUnloaded+=UnloadChunk;
        foreach(Vector2I chunk in _county.LoadedChunks)LoadChunk(chunk);
        RebuildPaths();
    }

    public override void _ExitTree()
    {
        if(GodotObject.IsInstanceValid(_county)){_county.ChunkLoaded-=LoadChunk;_county.ChunkUnloaded-=UnloadChunk;}
    }

    public void RefreshDocument(AuthoredCountyDocument document)
    {
        _document=document;foreach((Vector2I chunk,AuthoredTerrainChunkVisual visual) in _active)visual.SetStamps(_document.TerrainStamps.Where(item=>CountyCoordinateSpace.GridToChunk(new Vector2(item.X,item.Y))==chunk).ToArray());RebuildPaths();
    }
    public void SetLayerVisibility(string layer,bool visible)
    {
        if(layer=="Terrain"){_terrainVisible=visible;foreach(AuthoredTerrainChunkVisual visual in _active.Values)visual.Visible=visible;}
        if(layer=="Roads"){_roadsVisible=visible;foreach(AuthoredPathVisual visual in _paths.Values)visual.Visible=visible;if(_junctions is not null)_junctions.Visible=visible;}
    }

    private void LoadChunk(Vector2I coordinate)
    {
        if(_active.ContainsKey(coordinate))return;List<AuthoredTerrainStampData> stamps=_document.TerrainStamps.Where(item=>CountyCoordinateSpace.GridToChunk(new Vector2(item.X,item.Y))==coordinate).ToList();
        AuthoredTerrainChunkVisual visual=new(){Name=$"AuthoredTerrain_{coordinate.X}_{coordinate.Y}",Visible=_terrainVisible};visual.Initialize(stamps);_world.AddChild(visual);_world.MoveChild(visual,0);_active[coordinate]=visual;RefreshPathStreaming();
    }

    private void UnloadChunk(Vector2I coordinate){if(_active.Remove(coordinate,out AuthoredTerrainChunkVisual? visual)&&GodotObject.IsInstanceValid(visual))visual.QueueFree();RefreshPathStreaming();}
    private void RebuildPaths()
    {
        HashSet<string> required=_document.Paths.Where(path=>path.Points.Count>1).Select(path=>path.Id).ToHashSet();foreach(string id in _paths.Keys.Where(id=>!required.Contains(id)).ToArray()){_paths[id].QueueFree();_paths.Remove(id);}foreach(AuthoredPathData path in _document.Paths.Where(path=>path.Points.Count>1)){if(_paths.TryGetValue(path.Id,out AuthoredPathVisual? existing)){existing.SetPath(path);continue;}AuthoredPathVisual visual=new(){Name="AuthoredPath_"+path.Id,Visible=_roadsVisible};visual.Initialize(path);_world.AddChild(visual);_world.MoveChild(visual,0);_paths[path.Id]=visual;}
        if(_junctions is null){_junctions=new RoadJunctionVisual{Name="AuthoredRoadJunctions",Visible=_roadsVisible};_world.AddChild(_junctions);_world.MoveChild(_junctions,0);}
        _junctions.SetDocument(_document);RefreshPathStreaming();
    }
    private void RefreshPathStreaming(){IReadOnlyCollection<Vector2I> chunks=_county?.LoadedChunks??Array.Empty<Vector2I>();foreach(AuthoredPathVisual visual in _paths.Values)visual.SetLoadedChunks(chunks);_junctions?.SetLoadedChunks(chunks);}
}

internal partial class AuthoredPathVisual : Node2D
{
    private AuthoredPathData _path=null!;
    private HashSet<Vector2I> _loadedChunks=[];
    private int _signature;
    public void Initialize(AuthoredPathData path){_path=path;_signature=Signature(path);ZAsRelative=false;ZIndex=-68;TextureRepeat=TextureRepeatEnum.Enabled;}
    public void SetPath(AuthoredPathData path){int signature=Signature(path);_path=path;if(signature==_signature)return;_signature=signature;QueueRedraw();}
    public void SetLoadedChunks(IEnumerable<Vector2I> chunks){_loadedChunks=chunks.ToHashSet();QueueRedraw();}
    public override void _Ready()=>QueueRedraw();
    public override void _Draw()
    {
        if(_path.Points.Count<2)return;if(_path.LineKind=="Structure"){DrawStructureLine();return;}
        RoadProfileDefinition profile=RoadProfiles.Get(_path.PathType);IReadOnlyList<RoadSplineSample> samples=RoadSplineGeometry.Sample(_path);if(samples.Count<2)return;
        Texture2D? texture=ResourceLoader.Exists(profile.SurfaceTexture)?TextureRegistry.Get(profile.SurfaceTexture):null;
        for(int i=0;i<samples.Count-1;i++)
        {
            RoadSplineSample a=samples[i],b=samples[i+1];if(!SegmentLoaded(a.GridPosition,b.GridPosition)||a.CanvasPosition.DistanceSquaredTo(b.CanvasPosition)<.01f)continue;
            float halfA=Mathf.Max(2,a.Width*IsometricGrid.TileHeight*.5f),halfB=Mathf.Max(2,b.Width*IsometricGrid.TileHeight*.5f);
            Vector2 normalA=new(-a.CanvasTangent.Y,a.CanvasTangent.X),normalB=new(-b.CanvasTangent.Y,b.CanvasTangent.X);
            bool organicEdge=profile.SurfaceTexture.Contains("dirt")||profile.SurfaceTexture.Contains("mud");float shoulderA=profile.ShoulderScale*(organicEdge ? .96f+Deterministic01(_path.VariationSeed,i)*.08f : 1),shoulderB=profile.ShoulderScale*(organicEdge ? .96f+Deterministic01(_path.VariationSeed,i+1)*.08f : 1);Vector2[] shoulder=[a.CanvasPosition-normalA*halfA*shoulderA,a.CanvasPosition+normalA*halfA*shoulderA,b.CanvasPosition+normalB*halfB*shoulderB,b.CanvasPosition-normalB*halfB*shoulderB];
            DrawColoredQuad(shoulder,profile.Shoulder);
            Vector2[] surface=[a.CanvasPosition-normalA*halfA,a.CanvasPosition+normalA*halfA,b.CanvasPosition+normalB*halfB,b.CanvasPosition-normalB*halfB];
            if(texture is null)DrawColoredQuad(surface,profile.Surface);else
            {
                float repeat=96;Vector2[] uv=[new(0,a.Distance/repeat),new(1,a.Distance/repeat),new(1,b.Distance/repeat),new(0,b.Distance/repeat)];
                float variation=.94f+Deterministic01(_path.VariationSeed,i)*.09f;DrawTexturedQuad(surface,uv,texture,new Color(variation,variation,variation,1));
            }
        }
        DrawMarkings(samples,profile);
    }
    private void DrawStructureLine()
    {
        if(!ResourceLoader.Exists(_path.AssetPath))return;Texture2D texture=TextureRegistry.Get(_path.AssetPath);float scale=Mathf.Max(.04f,_path.SegmentScale);Vector2 size=texture.GetSize()*scale;
        for(int segment=0;segment<_path.Points.Count-1;segment++){Vector2 a=_path.Points[segment].Vector,b=_path.Points[segment+1].Vector;int count=Mathf.Max(1,Mathf.CeilToInt(a.DistanceTo(b)/Mathf.Max(.25f,_path.SegmentSpacing)));for(int i=0;i<=count;i++){Vector2 point=a.Lerp(b,i/(float)count);DrawSetTransform(IsometricGrid.GridToScreen(point));DrawTextureRect(texture,new Rect2(new Vector2(-size.X*.5f,-size.Y),size),false);}}DrawSetTransform(Vector2.Zero);
    }
    private void DrawMarkings(IReadOnlyList<RoadSplineSample> samples,RoadProfileDefinition profile)
    {
        if(profile.Markings==RoadMarkingStyle.None)return;
        Color yellow=new("d4b64ecc"),white=new("e7e2d1c8");float line=Mathf.Max(1.2f,_path.Width*1.45f);
        for(int i=0;i<samples.Count-1;i++)
        {
            RoadSplineSample a=samples[i],b=samples[i+1];if(!SegmentLoaded(a.GridPosition,b.GridPosition))continue;
            bool dash=((int)(a.Distance/34))%2==0;Vector2 na=new(-a.CanvasTangent.Y,a.CanvasTangent.X),nb=new(-b.CanvasTangent.Y,b.CanvasTangent.X);
            if(profile.Markings is RoadMarkingStyle.FarmRuts or RoadMarkingStyle.LoggingRuts)
            {
                float offsetA=a.Width*IsometricGrid.TileHeight*.23f,offsetB=b.Width*IsometricGrid.TileHeight*.23f;Color rut=profile.Markings==RoadMarkingStyle.LoggingRuts?new Color("493b31a8"):new Color("68513aa0");float rutWidth=profile.Markings==RoadMarkingStyle.LoggingRuts?3.2f:2.2f;DrawLine(a.CanvasPosition-na*offsetA,b.CanvasPosition-nb*offsetB,rut,rutWidth,true);DrawLine(a.CanvasPosition+na*offsetA,b.CanvasPosition+nb*offsetB,rut,rutWidth,true);if(profile.Markings==RoadMarkingStyle.FarmRuts)DrawLine(a.CanvasPosition,b.CanvasPosition,new Color("66704872"),Mathf.Max(2,a.Width*IsometricGrid.TileHeight*.12f),true);continue;
            }
            if(profile.Markings==RoadMarkingStyle.SingleCenter&&dash)DrawLine(a.CanvasPosition,b.CanvasPosition,yellow,line,true);
            if(profile.Markings is RoadMarkingStyle.DoubleCenter or RoadMarkingStyle.Highway)
            {
                float gap=Mathf.Max(2,line*1.25f);DrawLine(a.CanvasPosition-na*gap,b.CanvasPosition-nb*gap,yellow,line,true);DrawLine(a.CanvasPosition+na*gap,b.CanvasPosition+nb*gap,yellow,line,true);
            }
            if(profile.Markings==RoadMarkingStyle.Highway)
            {
                float edgeA=a.Width*IsometricGrid.TileHeight*.39f,edgeB=b.Width*IsometricGrid.TileHeight*.39f;
                DrawLine(a.CanvasPosition-na*edgeA,b.CanvasPosition-nb*edgeB,white,line,true);DrawLine(a.CanvasPosition+na*edgeA,b.CanvasPosition+nb*edgeB,white,line,true);
            }
        }
    }
    private bool SegmentLoaded(Vector2 a,Vector2 b)=>_loadedChunks.Count==0||_loadedChunks.Contains(CountyCoordinateSpace.GridToChunk((a+b)*.5f));
    private void DrawColoredQuad(Vector2[] quad,Color color){DrawColoredPolygon([quad[0],quad[1],quad[2]],color);DrawColoredPolygon([quad[0],quad[2],quad[3]],color);}
    private void DrawTexturedQuad(Vector2[] quad,Vector2[] uv,Texture2D texture,Color tint){Color[] colors=[tint];Vector2[] first=[quad[0],quad[1],quad[2]],firstUv=[uv[0],uv[1],uv[2]],second=[quad[0],quad[2],quad[3]],secondUv=[uv[0],uv[2],uv[3]];DrawPolygon(first,colors,firstUv,texture);DrawPolygon(second,colors,secondUv,texture);}
    private static float Deterministic01(int seed,int segment){unchecked{uint x=(uint)(seed*374761393+segment*668265263);x=(x^(x>>13))*1274126177u;return (x&0xffff)/65535f;}}
    private static int Signature(AuthoredPathData path){HashCode hash=new();hash.Add(path.PathType);hash.Add(path.LineKind);hash.Add(path.AssetPath);hash.Add(path.Width);hash.Add(path.SegmentSpacing);hash.Add(path.SegmentScale);hash.Add(path.VariationSeed);foreach(AuthoredPointData point in path.Points){hash.Add(point.X);hash.Add(point.Y);hash.Add(point.WidthScale);}return hash.ToHashCode();}
}

internal partial class RoadJunctionVisual : Node2D
{
    private AuthoredCountyDocument _document=new();private RoadNetworkGraph _graph=new();private HashSet<Vector2I> _loadedChunks=[];private int _signature;
    public RoadJunctionVisual(){ZAsRelative=false;ZIndex=-69;TextureRepeat=TextureRepeatEnum.Enabled;}
    public void SetDocument(AuthoredCountyDocument document){int signature=Signature(document);_document=document;if(signature==_signature)return;_signature=signature;_graph=RoadNetworkGraph.Build(document);QueueRedraw();}
    public void SetLoadedChunks(IEnumerable<Vector2I> chunks){_loadedChunks=chunks.ToHashSet();QueueRedraw();}
    public override void _Draw()
    {
        foreach(RoadGraphNode node in _graph.Nodes.Where(node=>node.Degree>=3))
        {
            if(_loadedChunks.Count>0&&!_loadedChunks.Contains(CountyCoordinateSpace.GridToChunk(node.Position)))continue;
            AuthoredPathData? path=_document.Paths.Where(item=>node.PathIds.Contains(item.Id)).MaxBy(item=>item.Width);if(path is null)continue;RoadProfileDefinition profile=RoadProfiles.Get(path.PathType);float radius=Mathf.Max(5,path.Width*IsometricGrid.TileHeight*.5f);Vector2 center=IsometricGrid.GridToScreen(node.Position);DrawCircle(center,radius*profile.ShoulderScale,profile.Shoulder);Vector2[] polygon=new Vector2[24],uv=new Vector2[24];for(int i=0;i<polygon.Length;i++){float angle=Mathf.Tau*i/polygon.Length;polygon[i]=center+Vector2.FromAngle(angle)*radius;uv[i]=polygon[i]/96f;}if(ResourceLoader.Exists(profile.SurfaceTexture)){Color[] colors=[Colors.White];DrawPolygon(polygon,colors,uv,TextureRegistry.Get(profile.SurfaceTexture));}else DrawColoredPolygon(polygon,profile.Surface);
        }
    }
    private static int Signature(AuthoredCountyDocument document){HashCode hash=new();foreach(AuthoredPathData path in document.Paths.Where(path=>path.LineKind=="Road")){hash.Add(path.Id);hash.Add(path.PathType);hash.Add(path.Width);foreach(AuthoredPointData point in path.Points){hash.Add(point.X);hash.Add(point.Y);hash.Add(point.WidthScale);}}foreach(AuthoredWorldObjectData item in document.WorldObjects.Where(item=>item.AssetPath.Contains("/bridges/"))){hash.Add(item.Id);hash.Add(item.X);hash.Add(item.Y);hash.Add(item.Scale);hash.Add(item.RotationDegrees);}return hash.ToHashCode();}
}

internal partial class AuthoredTerrainChunkVisual : Node2D
{
    private IReadOnlyList<AuthoredTerrainStampData> _stamps=[];
    public void Initialize(IReadOnlyList<AuthoredTerrainStampData> stamps){_stamps=stamps;ZAsRelative=false;ZIndex=-70;}
    public void SetStamps(IReadOnlyList<AuthoredTerrainStampData> stamps){_stamps=stamps;QueueRedraw();}
    public override void _Ready()=>QueueRedraw();
    public override void _Draw()
    {
        foreach(AuthoredTerrainStampData stamp in _stamps)
        {
            if(!ResourceLoader.Exists(stamp.AssetPath))continue;Texture2D texture=TextureRegistry.Get(stamp.AssetPath);float width=Mathf.Max(48,stamp.Radius*IsometricGrid.TileWidth);float scale=width/Mathf.Max(1,texture.GetWidth());Vector2 size=texture.GetSize()*scale;Vector2 center=IsometricGrid.GridToScreen(new Vector2(stamp.X,stamp.Y));
            DrawSetTransform(center,Mathf.DegToRad(stamp.RotationDegrees));DrawTextureRect(texture,new Rect2(-size*.5f,size),false,new Color(1,1,1,Mathf.Clamp(stamp.Opacity,.05f,1)));
        }
        DrawSetTransform(Vector2.Zero);
    }
}
