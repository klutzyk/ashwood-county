#nullable enable

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
        if(layer=="Roads"){_roadsVisible=visible;foreach(AuthoredPathVisual visual in _paths.Values)visual.Visible=visible;}
    }

    private void LoadChunk(Vector2I coordinate)
    {
        if(_active.ContainsKey(coordinate))return;List<AuthoredTerrainStampData> stamps=_document.TerrainStamps.Where(item=>CountyCoordinateSpace.GridToChunk(new Vector2(item.X,item.Y))==coordinate).ToList();
        AuthoredTerrainChunkVisual visual=new(){Name=$"AuthoredTerrain_{coordinate.X}_{coordinate.Y}",Visible=_terrainVisible};visual.Initialize(stamps);_world.AddChild(visual);_world.MoveChild(visual,0);_active[coordinate]=visual;
    }

    private void UnloadChunk(Vector2I coordinate){if(_active.Remove(coordinate,out AuthoredTerrainChunkVisual? visual)&&GodotObject.IsInstanceValid(visual))visual.QueueFree();}
    private void RebuildPaths()
    {
        HashSet<string> required=_document.Paths.Where(path=>path.Points.Count>1).Select(path=>path.Id).ToHashSet();foreach(string id in _paths.Keys.Where(id=>!required.Contains(id)).ToArray()){_paths[id].QueueFree();_paths.Remove(id);}foreach(AuthoredPathData path in _document.Paths.Where(path=>path.Points.Count>1)){if(_paths.TryGetValue(path.Id,out AuthoredPathVisual? existing)){existing.SetPath(path);continue;}AuthoredPathVisual visual=new(){Name="AuthoredPath_"+path.Id,Visible=_roadsVisible};visual.Initialize(path);_world.AddChild(visual);_world.MoveChild(visual,0);_paths[path.Id]=visual;}
    }
}

internal partial class AuthoredPathVisual : Node2D
{
    private AuthoredPathData _path=null!;
    public void Initialize(AuthoredPathData path){_path=path;ZAsRelative=false;ZIndex=-68;}
    public void SetPath(AuthoredPathData path){_path=path;QueueRedraw();}
    public override void _Ready()=>QueueRedraw();
    public override void _Draw()
    {
        Vector2[] points=_path.Points.Select(point=>IsometricGrid.GridToScreen(point.Vector)).ToArray();if(points.Length<2)return;if(_path.LineKind=="Structure"){DrawStructureLine();return;}float pixels=Mathf.Max(5,_path.Width*IsometricGrid.TileHeight);bool dirt=_path.PathType.Contains("Dirt")||_path.PathType.Contains("Track")||_path.PathType.Contains("Foot");Color shoulder=dirt?new Color("756346d9"):new Color("6f6657e6"),surface=dirt?new Color("9b8258ef"):new Color("555957f2");DrawPolyline(points,shoulder,pixels*1.34f,true);DrawPolyline(points,surface,pixels,true);if(!dirt&&pixels>20)DrawPolyline(points,new Color("c6b36e9a"),Mathf.Max(1.5f,pixels*.045f),true);DrawRoadArt();
    }
    private void DrawRoadArt()
    {
        if(string.IsNullOrWhiteSpace(_path.AssetPath)||!ResourceLoader.Exists(_path.AssetPath))return;Texture2D texture=TextureRegistry.Get(_path.AssetPath);float targetWidth=Mathf.Max(12,_path.Width*IsometricGrid.TileHeight*1.15f);float scale=targetWidth/Mathf.Max(1,texture.GetHeight());Vector2 size=texture.GetSize()*scale;
        for(int segment=0;segment<_path.Points.Count-1;segment++){Vector2 a=IsometricGrid.GridToScreen(_path.Points[segment].Vector),b=IsometricGrid.GridToScreen(_path.Points[segment+1].Vector);float length=a.DistanceTo(b);int count=Mathf.Max(1,Mathf.CeilToInt(length/Mathf.Max(18,size.X*.65f)));float angle=(b-a).Angle();for(int i=0;i<=count;i++){Vector2 point=a.Lerp(b,i/(float)count);DrawSetTransform(point,angle);DrawTextureRect(texture,new Rect2(-size*.5f,size),false,new Color(1,1,1,.94f));}}DrawSetTransform(Vector2.Zero);
    }
    private void DrawStructureLine()
    {
        if(!ResourceLoader.Exists(_path.AssetPath))return;Texture2D texture=TextureRegistry.Get(_path.AssetPath);float scale=Mathf.Max(.04f,_path.SegmentScale);Vector2 size=texture.GetSize()*scale;
        for(int segment=0;segment<_path.Points.Count-1;segment++){Vector2 a=_path.Points[segment].Vector,b=_path.Points[segment+1].Vector;int count=Mathf.Max(1,Mathf.CeilToInt(a.DistanceTo(b)/Mathf.Max(.25f,_path.SegmentSpacing)));for(int i=0;i<=count;i++){Vector2 point=a.Lerp(b,i/(float)count);DrawSetTransform(IsometricGrid.GridToScreen(point));DrawTextureRect(texture,new Rect2(new Vector2(-size.X*.5f,-size.Y),size),false);}}DrawSetTransform(Vector2.Zero);
    }
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
