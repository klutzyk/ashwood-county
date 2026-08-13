#nullable enable

using System;
using System.Linq;
using AshwoodCounty.Camera;
using AshwoodCounty.Units;
using AshwoodCounty.World.Fog;
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
        ["outskirts_road"] = new Vector2(197, 166)
    };

    private static readonly Vector2[] Route=[new(180,190),new(160,232),new(203,157)];
    private Survivor _survivor=null!;private CountyProgress _progress=null!;private CountyFogOfWar _fog=null!;private int _leg;private Vector2 _last;private float _originalSpeed;private double _elapsed;private bool _active;
    public override void _Ready(){if(System.Environment.GetEnvironmentVariable("ASHWOOD_CAPTURE_COUNTY_MAP")=="1"){Callable.From(OpenMap).CallDeferred();SetProcess(false);return;}string? capture=System.Environment.GetEnvironmentVariable("ASHWOOD_CAPTURE_LOCATION");if(!string.IsNullOrWhiteSpace(capture)){Callable.From(()=>ConfigureCapture(capture)).CallDeferred();SetProcess(false);return;}if(System.Environment.GetEnvironmentVariable("ASHWOOD_VALIDATE_CONTINUOUS_WORLD")!="1"){SetProcess(false);return;}Callable.From(Begin).CallDeferred();}
    private void OpenMap()=>Input.ParseInputEvent(new InputEventKey{Keycode=Key.M,Pressed=true});
    private void ConfigureCapture(string capture)
    {
        if (!CaptureLocations.TryGetValue(capture, out Vector2 gridPosition))
        {
            GD.PushWarning($"Unknown ASHWOOD_CAPTURE_LOCATION '{capture}'.");
            return;
        }

        StrategyCamera camera = GetNode<StrategyCamera>("../World/StrategyCamera");
        camera.CenterOnGridPosition(gridPosition);
        camera.SetZoom(capture is "ashwood" ? .48f : .57f);
        CountyFogOfWar fog = GetNode<CountyFogOfWar>("../World/CountyFog");
        fog.DebugMode = FogDebugMode.RevealAll;
        GD.Print($"VISUAL_CAPTURE: {capture} at {gridPosition}");
    }
    private void Begin(){_survivor=GetTree().GetNodesInGroup(Survivor.GroupName).OfType<Survivor>().First();_progress=GetNode<CountyProgress>("../CountyProgress");_fog=GetNode<CountyFogOfWar>("../World/CountyFog");_originalSpeed=_survivor.MovementSpeed;_survivor.MovementSpeed=12;_last=_survivor.SimulationPosition;_active=true;_survivor.IssueMoveOrder(Route[0]);GD.Print("CONTINUOUS_WORLD_VALIDATION: physical traversal started");}
    public override void _Process(double delta){if(!_active)return;_elapsed+=delta;float travelled=_last.DistanceTo(_survivor.SimulationPosition);float permittedStep=_survivor.MovementSpeed*(float)delta*1.35f+.35f;if(delta>.001&&travelled>Mathf.Max(permittedStep,1.1f)){Finish(false,$"teleport-sized movement delta {travelled:0.00}");return;}_last=_survivor.SimulationPosition;if(_last.DistanceTo(Route[_leg])>.12f){if(_elapsed>100)Finish(false,"timeout");return;}_leg++;if(_leg<Route.Length){_survivor.IssueMoveOrder(Route[_leg]);return;}bool farm=_progress.GetState("farm_district").Discovered;bool mill=_progress.GetState("mill_creek").Discovered;bool fog=_fog.IsExplored(new Vector2(180,190))&&_fog.IsExplored(new Vector2(160,232));Finish(farm&&mill&&fog,$"farm={farm}, mill={mill}, fog_route={fog}");}
    private void Finish(bool passed,string detail){_active=false;_survivor.MovementSpeed=_originalSpeed;GD.Print($"CONTINUOUS_WORLD_VALIDATION: {(passed?"PASS":"FAIL")} ({detail})");SetProcess(false);}
}
