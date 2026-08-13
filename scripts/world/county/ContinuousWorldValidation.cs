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
        ["river"] = new Vector2(187, 193),
        ["railway"] = new Vector2(153, 251)
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
        camera.SetZoom(capture == "ashwood" ? .48f : .72f);
        CountyFogOfWar fog = GetNode<CountyFogOfWar>("../World/CountyFog");
        fog.DebugMode = FogDebugMode.RevealAll;
        GD.Print($"VISUAL_CAPTURE: {capture} at {gridPosition}");
    }
    private void Begin(){_survivor=GetTree().GetNodesInGroup(Survivor.GroupName).OfType<Survivor>().First();_progress=GetNode<CountyProgress>("../CountyProgress");_fog=GetNode<CountyFogOfWar>("../World/CountyFog");_originalSpeed=_survivor.MovementSpeed;_survivor.MovementSpeed=48;_last=_survivor.SimulationPosition;_active=true;_survivor.IssueMoveOrder(Route[0]);GD.Print("CONTINUOUS_WORLD_VALIDATION: physical traversal started");}
    public override void _Process(double delta){if(!_active)return;_elapsed+=delta;float travelled=_last.DistanceTo(_survivor.SimulationPosition);if(delta>.001&&travelled>_survivor.MovementSpeed*(float)delta*1.35f+.35f){Finish(false,$"teleport-sized movement delta {travelled:0.00}");return;}_last=_survivor.SimulationPosition;if(_last.DistanceTo(Route[_leg])>.12f){if(_elapsed>25)Finish(false,"timeout");return;}_leg++;if(_leg<Route.Length){_survivor.IssueMoveOrder(Route[_leg]);return;}bool farm=_progress.GetState("farm_district").Discovered;bool mill=_progress.GetState("mill_creek").Discovered;bool fog=_fog.IsExplored(new Vector2(180,190))&&_fog.IsExplored(new Vector2(160,232));Finish(farm&&mill&&fog,$"farm={farm}, mill={mill}, fog_route={fog}");}
    private void Finish(bool passed,string detail){_active=false;_survivor.MovementSpeed=_originalSpeed;GD.Print($"CONTINUOUS_WORLD_VALIDATION: {(passed?"PASS":"FAIL")} ({detail})");SetProcess(false);}
}
