#nullable enable

using System;
using System.Linq;
using AshwoodCounty.Camera;
using AshwoodCounty.Units;
using AshwoodCounty.World.Fog;
using AshwoodCounty.Buildings.Interiors;
using Godot;

namespace AshwoodCounty.World.County;

/// <summary>Opt-in sustained traversal check. Set ASHWOOD_VALIDATE_CONTINUOUS_WORLD=1; inert in normal play.</summary>
public partial class ContinuousWorldValidation:Node
{
    private static readonly System.Collections.Generic.Dictionary<string, Vector2> CaptureLocations = new(System.StringComparer.OrdinalIgnoreCase)
    {
        ["camp"] = CountyCoordinateSpace.StartingCamp,
        ["farm_transition"] = new Vector2(187, 180),
        ["farm"] = new Vector2(170, 204),
        ["mill_transition"] = new Vector2(160, 232),
        ["mill"] = new Vector2(154, 250),
        ["ashwood"] = new Vector2(252, 145),
        ["blackwater"] = new Vector2(246, 62),
        ["blackwater_shore"] = new Vector2(222, 84),
        ["river"] = new Vector2(187, 193),
        ["railway"] = new Vector2(153, 251),
        ["south_farmland"] = new Vector2(164, 270),
        ["trailer_park"] = new Vector2(279, 211),
        ["fairgrounds"] = new Vector2(246, 234),
        ["service_station"] = new Vector2(226, 190),
        ["hospital"] = new Vector2(244, 151),
        ["sheriff"] = new Vector2(272, 137),
        ["main_street"] = new Vector2(252, 145),
        ["residential"] = new Vector2(274, 160),
        ["old_mill_bridge"] = new Vector2(166, 121),
        ["dam"] = new Vector2(301, 103),
        ["logging_camp"] = new Vector2(105, 74),
        ["pine_ridge"] = new Vector2(72, 37),
        ["fire_lookout"] = new Vector2(311, 54),
        ["highway"] = new Vector2(307, 137),
        ["outskirts_road"] = new Vector2(197, 166),
        ["interior_house"] = new Vector2(220, 155)
    };

    private static readonly Vector2[] Route=[new(180,190),new(160,232),new(203,157)];
    private Survivor _survivor=null!;private CountyProgress _progress=null!;private CountyFogOfWar _fog=null!;private int _leg;private Vector2 _last;private float _originalSpeed;private double _elapsed;private bool _active;
    public override void _Ready(){if(System.Environment.GetEnvironmentVariable("ASHWOOD_CAPTURE_INTERIOR")=="1"){ConfigureInteriorCapture();SetProcess(false);return;}if(System.Environment.GetEnvironmentVariable("ASHWOOD_CAPTURE_COUNTY_MAP")=="1"){Callable.From(OpenMap).CallDeferred();SetProcess(false);return;}string? capture=System.Environment.GetEnvironmentVariable("ASHWOOD_CAPTURE_LOCATION");if(!string.IsNullOrWhiteSpace(capture)){Callable.From(()=>ConfigureCapture(capture)).CallDeferred();SetProcess(false);return;}if(System.Environment.GetEnvironmentVariable("ASHWOOD_VALIDATE_CONTINUOUS_WORLD")!="1"){SetProcess(false);return;}Callable.From(Begin).CallDeferred();}
    private void OpenMap()=>Input.ParseInputEvent(new InputEventKey{Keycode=Key.M,Pressed=true});
    private void ConfigureCapture(string capture)
    {
        if (!CaptureLocations.TryGetValue(capture, out Vector2 gridPosition))
        {
            GD.PushWarning($"Unknown ASHWOOD_CAPTURE_LOCATION '{capture}'.");
            return;
        }

        StrategyCamera camera = GetNode<StrategyCamera>("../World/StrategyCamera");
        camera.SnapTo(gridPosition, capture is "interior_house" ? .82f : capture is "ashwood" ? .48f : .57f);

        // Optional wall-clock override so a capture can be taken at dusk or at
        // night without waiting out a real day cycle.
        string? hour = System.Environment.GetEnvironmentVariable("ASHWOOD_CAPTURE_HOUR");
        if (!string.IsNullOrWhiteSpace(hour) && double.TryParse(hour, System.Globalization.CultureInfo.InvariantCulture, out double parsed))
            GetNode<AshwoodCounty.Systems.GameClock>("../GameClock").SetTotalMinutes(parsed * 60d);
        CountyFogOfWar fog = GetNode<CountyFogOfWar>("../World/CountyFog");
        fog.DebugMode = FogDebugMode.RevealAll;
        GD.Print($"VISUAL_CAPTURE: {capture} at {gridPosition}");
        string? pngPath = System.Environment.GetEnvironmentVariable("ASHWOOD_CAPTURE_PNG");
        if (!string.IsNullOrWhiteSpace(pngPath)) CapturePngAfterFrames(pngPath);
    }
    private async void CapturePngAfterFrames(string path)
    {
        // Terrain detail streams from the visible rectangle, so the capture has
        // to give the layers a couple of refresh ticks to build.
        for(int i=0;i<90;i++)await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);
        ReportRenderCost();
        Error error=GetViewport().GetTexture().GetImage().SavePng(path);
        GD.Print($"VISUAL_CAPTURE_PNG: {error} {path}");
    }
    /// <summary>
    /// Renderer cost at the moment of capture. The terrain layers stream chunks
    /// from the visible rectangle, so this is the number that matters when
    /// judging whether an environment-art pass has become too expensive.
    /// </summary>
    private void ReportRenderCost()
    {
        ulong objects=RenderingServer.GetRenderingInfo(RenderingServer.RenderingInfo.TotalObjectsInFrame);
        ulong draws=RenderingServer.GetRenderingInfo(RenderingServer.RenderingInfo.TotalDrawCallsInFrame);
        ulong primitives=RenderingServer.GetRenderingInfo(RenderingServer.RenderingInfo.TotalPrimitivesInFrame);
        int nodes=GetTree().GetNodeCount();
        GD.Print($"RENDER_COST: fps={Engine.GetFramesPerSecond()} nodes={nodes} objects={objects} draw_calls={draws} primitives={primitives}");
    }

    private async void ConfigureInteriorCapture()
    {
        for(int i=0;i<4;i++)await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);
        StrategyCamera camera=GetNode<StrategyCamera>("../World/StrategyCamera");camera.CenterOnGridPosition(new Vector2(220,155));camera.SetZoom(.92f);
        GetNode<CountyFogOfWar>("../World/CountyFog").DebugMode=FogDebugMode.RevealAll;
        InteriorBuildingRuntime building=GetTree().GetNodesInGroup(InteriorBuildingRuntime.GroupName).OfType<InteriorBuildingRuntime>().First();
        foreach(string room in new[]{"living","kitchen","hall","bedroom_one"})building.State.DiscoveredRooms.Add(room);
        foreach(DoorRuntimeState door in building.State.Doors.Values)door.State=InteriorDoorState.Open;
        Survivor[] survivors=GetTree().GetNodesInGroup(Survivor.GroupName).OfType<Survivor>().Take(3).ToArray();
        survivors[0].SimulationPosition=new Vector2(218.2f,155.7f);survivors[1].SimulationPosition=new Vector2(218.2f,153.1f);survivors[2].SimulationPosition=new Vector2(221.7f,152.8f);
        for(int i=0;i<35;i++)await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);
        string? path=System.Environment.GetEnvironmentVariable("ASHWOOD_CAPTURE_PNG");
        if(!string.IsNullOrWhiteSpace(path)){Error error=GetViewport().GetTexture().GetImage().SavePng(path);GD.Print($"INTERIOR_CAPTURE_PNG: {error} {path}");}
    }
    private void Begin(){_survivor=GetTree().GetNodesInGroup(Survivor.GroupName).OfType<Survivor>().First();_progress=GetNode<CountyProgress>("../CountyProgress");_fog=GetNode<CountyFogOfWar>("../World/CountyFog");_originalSpeed=_survivor.MovementSpeed;_survivor.MovementSpeed=12;_last=_survivor.SimulationPosition;_active=true;_survivor.IssueMoveOrder(Route[0]);GD.Print("CONTINUOUS_WORLD_VALIDATION: physical traversal started");}
    public override void _Process(double delta){if(!_active)return;_elapsed+=delta;float travelled=_last.DistanceTo(_survivor.SimulationPosition);float permittedStep=_survivor.MovementSpeed*(float)delta*1.35f+.35f;if(delta>.001&&travelled>Mathf.Max(permittedStep,1.1f)){Finish(false,$"teleport-sized movement delta {travelled:0.00}");return;}_last=_survivor.SimulationPosition;if(_last.DistanceTo(Route[_leg])>.12f){if(_elapsed>100)Finish(false,"timeout");return;}_leg++;if(_leg<Route.Length){_survivor.IssueMoveOrder(Route[_leg]);return;}bool farm=_progress.GetState("farm_district").Discovered;bool mill=_progress.GetState("mill_creek").Discovered;bool fog=_fog.IsExplored(new Vector2(180,190))&&_fog.IsExplored(new Vector2(160,232));Finish(farm&&mill&&fog,$"farm={farm}, mill={mill}, fog_route={fog}");}
    private void Finish(bool passed,string detail){_active=false;_survivor.MovementSpeed=_originalSpeed;GD.Print($"CONTINUOUS_WORLD_VALIDATION: {(passed?"PASS":"FAIL")} ({detail})");SetProcess(false);}
}
