using AshwoodCounty.World.Fog;
using AshwoodCounty.World;
using AshwoodCounty.Buildings.Interiors;
using AshwoodCounty.UI;
using AshwoodCounty.Authoring;
using AshwoodCounty.Systems;
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
        if(world.GetNodeOrNull<NightLightingSystem>("NightLightingSystem") is null)
            world.AddChild(new NightLightingSystem{Name="NightLightingSystem"});
        Callable.From(CreateInteriorSystems).CallDeferred();
    }

    private void CreateInteriorSystems()
    {
        Node root=GetParent();
        if(root.GetNodeOrNull<WorldNavigationService>("WorldNavigationService") is null)
            root.AddChild(new WorldNavigationService{Name="WorldNavigationService"});
        if(root.GetNodeOrNull<OcclusionController>("OcclusionController") is null)
            root.AddChild(new OcclusionController{Name="OcclusionController"});
        if(root.GetNodeOrNull<InteriorNavigationService>("InteriorNavigationService") is null)
            root.AddChild(new InteriorNavigationService{Name="InteriorNavigationService"});
        if(root.GetNodeOrNull<InteriorBuildingSystem>("InteriorBuildingSystem") is null)
            root.AddChild(new InteriorBuildingSystem{Name="InteriorBuildingSystem"});
        if(root.GetNodeOrNull<InteriorContextHud>("InteriorContextHud") is null)
            root.AddChild(new InteriorContextHud{Name="InteriorContextHud"});
        if(root.GetNodeOrNull<InteractableHoverController>("InteractableHoverController") is null)
            root.AddChild(new InteractableHoverController{Name="InteractableHoverController"});
        if(root.GetNodeOrNull<SearchProgressOverlay>("SearchProgressOverlay") is null)
            root.AddChild(new SearchProgressOverlay{Name="SearchProgressOverlay"});
        if(root.GetNodeOrNull<SurvivalCycle>("SurvivalCycle") is null)
            root.AddChild(new SurvivalCycle{Name="SurvivalCycle"});
        if(root.GetNodeOrNull<StartingScenario>("StartingScenario") is null)
            root.AddChild(new StartingScenario{Name="StartingScenario"});
        if(root.GetNodeOrNull<SurvivalObjectives>("SurvivalObjectives") is null)
            root.AddChild(new SurvivalObjectives{Name="SurvivalObjectives"});
        if(root.GetNodeOrNull<SurvivalLoopValidation>("SurvivalLoopValidation") is null)
            root.AddChild(new SurvivalLoopValidation{Name="SurvivalLoopValidation"});
        if(root.GetNodeOrNull<EarlyGameCoreLoopValidation>("EarlyGameCoreLoopValidation") is null)
            root.AddChild(new EarlyGameCoreLoopValidation{Name="EarlyGameCoreLoopValidation"});
        if(root.GetNodeOrNull<TerrainStreamingValidation>("TerrainStreamingValidation") is null)
            root.AddChild(new TerrainStreamingValidation{Name="TerrainStreamingValidation"});
        if(root.GetNodeOrNull<AshwoodCounty.World.County.Visual.AssetInspector>("AssetInspector") is null)
            root.AddChild(new AshwoodCounty.World.County.Visual.AssetInspector{Name="AssetInspector"});
        if(root.GetNodeOrNull<InteriorVerticalSliceValidation>("InteriorVerticalSliceValidation") is null)
            root.AddChild(new InteriorVerticalSliceValidation{Name="InteriorVerticalSliceValidation"});
        if(root.GetNodeOrNull<ItemVerticalSliceValidation>("ItemVerticalSliceValidation") is null)
            root.AddChild(new ItemVerticalSliceValidation{Name="ItemVerticalSliceValidation"});
        if(root.GetNodeOrNull<ScavengeInteractionValidation>("ScavengeInteractionValidation") is null)
            root.AddChild(new ScavengeInteractionValidation{Name="ScavengeInteractionValidation"});
        if(root.GetNodeOrNull<WorkLoopValidation>("WorkLoopValidation") is null)
            root.AddChild(new WorkLoopValidation{Name="WorkLoopValidation"});
        if(root.GetNodeOrNull<AuthoredWorldObjectSystem>("AuthoredWorldObjectSystem") is null)
            root.AddChild(new AuthoredWorldObjectSystem{Name="AuthoredWorldObjectSystem"});
        if(root.GetNodeOrNull<AuthoredLandscapeSystem>("AuthoredLandscapeSystem") is null)
            root.AddChild(new AuthoredLandscapeSystem{Name="AuthoredLandscapeSystem"});
        if(AuthoringSessionState.IsPlaytesting&&root.GetNodeOrNull<AuthoringPlaytestController>("AuthoringPlaytestController") is null)
            root.AddChild(new AuthoringPlaytestController{Name="AuthoringPlaytestController"});
    }
}
