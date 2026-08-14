#nullable enable

using System.Linq;
using AshwoodCounty.Buildings.Interiors;
using AshwoodCounty.Camera;
using AshwoodCounty.UI;
using AshwoodCounty.Units;
using Godot;

namespace AshwoodCounty.Authoring;

/// <summary>Process-local handoff between Studio and the unmodified gameplay scene.</summary>
public static class AuthoringSessionState
{
    public static bool IsPlaytesting { get; set; }
    public static string BuildingId { get; set; } = string.Empty;
    public static Vector2 Center { get; set; } = new(220,155);
    public static int Radius { get; set; } = 1;
    public static bool ReturnToInterior { get; set; }
    public static string SelectionKind { get; set; } = string.Empty;
    public static string SelectionId { get; set; } = string.Empty;
    public static AuthoringTool ActiveTool { get; set; } = AuthoringTool.Select;
    public static float Zoom { get; set; } = .52f;
    public static bool AutomatedPlaytestStarted { get; set; }
    public static bool AutomatedPlaytestReturned { get; set; }
    public static bool AutomatedPlaytestPassed { get; set; }
}

public partial class AuthoringPlaytestController:CanvasLayer
{
    public override void _Ready()
    {
        if(!AuthoringSessionState.IsPlaytesting){QueueFree();return;}Layer=40;ProcessMode=ProcessModeEnum.Always;
        Button returnButton=new(){Text="F10  RETURN TO AUTHORING",Theme=AshwoodTheme.Create(),ThemeTypeVariation="HudActionButton",AnchorLeft=1,AnchorRight=1,OffsetLeft=-238,OffsetRight=-18,OffsetTop=86,OffsetBottom=124};returnButton.Pressed+=ReturnToStudio;AddChild(returnButton);
        Callable.From(ConfigurePlaytest).CallDeferred();
    }

    public override void _UnhandledInput(InputEvent inputEvent)
    {
        if(inputEvent is InputEventKey key&&key.Pressed&&!key.Echo&&key.Keycode==Key.F10){ReturnToStudio();GetViewport().SetInputAsHandled();}
    }

    private async void ConfigurePlaytest()
    {
        for(int i=0;i<4;i++)await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);
        AuthoredBuildingData? authored=AuthoredContentRepository.Load().Buildings.FirstOrDefault(item=>item.Id==AuthoringSessionState.BuildingId);if(authored is null)return;
        InteriorBuildingDefinition definition=AuthoredInteriorConverter.Convert(authored);DoorDefinition? entrance=definition.Doors.FirstOrDefault(door=>door.Exterior);
        Survivor? survivor=GetTree().GetNodesInGroup(Survivor.GroupName).OfType<Survivor>().FirstOrDefault();
        if(survivor is not null&&entrance is not null){survivor.SimulationPosition=entrance.OutsideApproachPoint+new Vector2(-.6f,.7f);survivor.IssueMoveOrder(entrance.OutsideApproachPoint);}
        Node? main=GetParent();StrategyCamera? camera=main?.GetNodeOrNull<StrategyCamera>("World/StrategyCamera");camera?.CenterOnGridPosition(new Vector2(authored.ExteriorX,authored.ExteriorY));camera?.SetZoom(.82f);
        (GetTree().GetFirstNodeInGroup(GameHud.GroupName) as GameHud)?.Notify("AUTHORING PLAYTEST\nNormal gameplay controls active • F10 returns to Studio");
        if(System.Environment.GetEnvironmentVariable("ASHWOOD_VALIDATE_AUTHORING_PLAYTEST")=="1"&&survivor is not null&&entrance is not null)
        {
            survivor.MovementSpeed=8;Vector2 target=definition.Rooms.First().Bounds.GetCenter();survivor.IssueMoveOrder(target);
            for(int frame=0;frame<1200&&survivor.SimulationPosition.DistanceTo(target)>.15f;frame++)await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);
            InteriorBuildingRuntime? runtime=GetTree().GetNodesInGroup(InteriorBuildingRuntime.GroupName).OfType<InteriorBuildingRuntime>().FirstOrDefault(item=>item.Definition.Id==definition.Id);
            AuthoringSessionState.AutomatedPlaytestPassed=survivor.SimulationPosition.DistanceTo(target)<=.15f&&runtime is not null&&runtime.State.Doors[entrance.Id].State==InteriorDoorState.Open;
            AuthoringSessionState.AutomatedPlaytestReturned=true;GD.Print($"AUTHORING_PLAYTEST_RUNTIME: {(AuthoringSessionState.AutomatedPlaytestPassed?"PASS":"FAIL")} (survivor={survivor.SimulationPosition}, target={target}, door={runtime?.State.Doors[entrance.Id].State})");ReturnToStudio();
        }
    }

    private void ReturnToStudio(){AuthoringSessionState.IsPlaytesting=false;GetTree().ChangeSceneToFile("res://scenes/tools/AuthoringStudio.tscn");}
}
