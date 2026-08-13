using AshwoodCounty.World.Fog;
using Godot;

namespace AshwoodCounty.World.County;

/// <summary>Creates county-scale renderers only in the running game, keeping the Godot editor lightweight.</summary>
public partial class CountyRuntimeBootstrap : Node
{
    public override void _EnterTree()
    {
        Node world=GetNode("../World");
        if(world.GetNodeOrNull<CountyWorld>("CountyWorld") is null)
        {
            CountyWorld county=new(){Name="CountyWorld",ZIndex=-20};
            world.AddChild(county);
            world.MoveChild(county,0);
        }
        if(world.GetNodeOrNull<CountyFogOfWar>("CountyFog") is null)
        {
            CountyFogOfWar fog=new(){Name="CountyFog",CountySize=new Vector2I(384,320),ChunkSize=20,SurvivorRevealRadius=8};
            world.AddChild(fog);
        }
    }
}
