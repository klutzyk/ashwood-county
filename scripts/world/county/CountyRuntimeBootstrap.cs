using AshwoodCounty.World.Fog;
using AshwoodCounty.Buildings.Interiors;
using AshwoodCounty.UI;
using AshwoodCounty.Authoring;
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
        Callable.From(CreateInteriorSystems).CallDeferred();
    }

    private void CreateInteriorSystems()
    {
        Node root=GetParent();
        if(root.GetNodeOrNull<InteriorNavigationService>("InteriorNavigationService") is null)
            root.AddChild(new InteriorNavigationService{Name="InteriorNavigationService"});
        if(root.GetNodeOrNull<InteriorBuildingSystem>("InteriorBuildingSystem") is null)
            root.AddChild(new InteriorBuildingSystem{Name="InteriorBuildingSystem"});
        if(root.GetNodeOrNull<InteriorContextHud>("InteriorContextHud") is null)
            root.AddChild(new InteriorContextHud{Name="InteriorContextHud"});
        if(root.GetNodeOrNull<InteriorVerticalSliceValidation>("InteriorVerticalSliceValidation") is null)
            root.AddChild(new InteriorVerticalSliceValidation{Name="InteriorVerticalSliceValidation"});
        if(root.GetNodeOrNull<AuthoredWorldObjectSystem>("AuthoredWorldObjectSystem") is null)
            root.AddChild(new AuthoredWorldObjectSystem{Name="AuthoredWorldObjectSystem"});
        if(root.GetNodeOrNull<AuthoredLandscapeSystem>("AuthoredLandscapeSystem") is null)
            root.AddChild(new AuthoredLandscapeSystem{Name="AuthoredLandscapeSystem"});
        if(AuthoringSessionState.IsPlaytesting&&root.GetNodeOrNull<AuthoringPlaytestController>("AuthoringPlaytestController") is null)
            root.AddChild(new AuthoringPlaytestController{Name="AuthoringPlaytestController"});
    }
}
