#nullable enable

using System.Collections.Generic;
using System.Linq;
using AshwoodCounty.World;
using AshwoodCounty.World.County;
using Godot;

namespace AshwoodCounty.Authoring;

/// <summary>Streams project-authored exterior objects with the county's active chunks.</summary>
public partial class AuthoredWorldObjectSystem : Node
{
    private readonly Dictionary<Vector2I,List<AuthoredWorldObjectVisual>> _active=[];
    private CountyWorld _county=null!;
    private Node2D _objects=null!;
    private AuthoredCountyDocument _document=null!;

    public override void _Ready()
    {
        _county=GetNode<CountyWorld>("../World/CountyWorld");
        _objects=GetNode<Node2D>("../World/Objects");
        _document=AuthoredContentRepository.Load();
        _county.ChunkLoaded+=LoadChunk;
        _county.ChunkUnloaded+=UnloadChunk;
        foreach(Vector2I chunk in _county.LoadedChunks)LoadChunk(chunk);
    }

    public override void _ExitTree()
    {
        if(GodotObject.IsInstanceValid(_county))
        {
            _county.ChunkLoaded-=LoadChunk;
            _county.ChunkUnloaded-=UnloadChunk;
        }
    }

    private void LoadChunk(Vector2I coordinate)
    {
        if(_active.ContainsKey(coordinate))return;
        List<AuthoredWorldObjectVisual> nodes=[];
        foreach(AuthoredWorldObjectData item in _document.WorldObjects.Where(item=>CountyCoordinateSpace.GridToChunk(new Vector2(item.X,item.Y))==coordinate))
        {
            AuthoredWorldObjectVisual visual=new(){Name="Authored_"+item.Id};
            visual.Initialize(item);_objects.AddChild(visual);nodes.Add(visual);
        }
        _active[coordinate]=nodes;
    }

    private void UnloadChunk(Vector2I coordinate)
    {
        if(!_active.Remove(coordinate,out List<AuthoredWorldObjectVisual>? nodes))return;
        foreach(AuthoredWorldObjectVisual node in nodes)if(GodotObject.IsInstanceValid(node))node.QueueFree();
    }
}

public partial class AuthoredWorldObjectVisual : Node2D
{
    private Texture2D _texture=null!;
    private AuthoredWorldObjectData _data=null!;
    public string PersistentId=>_data.Id;
    public AuthoredWorldObjectData Data=>_data;

    public void Initialize(AuthoredWorldObjectData data)
    {
        _data=data;_texture=TextureRegistry.Get(data.AssetPath);
        Position=IsometricGrid.GridToScreen(new Vector2(data.X,data.Y));
        RotationDegrees=data.RotationDegrees;
        ZIndex=0;
    }

    public override void _Ready()=>QueueRedraw();
    public override void _Draw()
    {
        Vector2 size=_texture.GetSize()*new Vector2(Mathf.Max(.02f,_data.Scale),Mathf.Max(.02f,_data.ScaleY>0?_data.ScaleY:_data.Scale));
        Vector2 origin=new(-size.X*Mathf.Clamp(_data.AnchorX,0,1),-size.Y*Mathf.Clamp(_data.AnchorY,0,1));
        DrawTextureRect(_texture,new Rect2(origin,size),false);
    }
}
